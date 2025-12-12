Shader "Custom/Grass_Indirect"
{
    Properties
    {
        [Header(Grass Colors)]
        _BaseColor ("Root Color (Shadow)", Color) = (0.1, 0.25, 0.1, 1)
        _TipColor ("Tip Color (Sun)", Color) = (0.5, 0.8, 0.1, 1)
        _ColorVar ("Blade Variation", Range(0, 0.5)) = 0.15

        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map (Top Down)", 2D) = "white" {}
        _TerrainSize ("Terrain Size (XZ)", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Position (XZ)", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.8

        [Header(Wind Physics)]
        // NOTE: WindMap, Speed, Scale sont maintenant gérés par GlobalWindManager.
        // On ne garde que le multiplicateur local pour ajuster la rigidité de cette herbe spécifique.
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.0

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.9, 1.0, 0.7, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.4

        [Header(Blade Variation)]
        _MinScale ("Min Scale Variance", Float) = 0.8
        _MaxScale ("Max Scale Variance", Float) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
        }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float4> _VisibleInstances;

            // --- VARIABLES GLOBALES (Gérées par GlobalWindManager.cs) ---
            // Elles ne sont pas dans le CBUFFER "UnityPerMaterial" car elles sont partagées par toute la scène.
            TEXTURE2D(_WindMap);
            SAMPLER(sampler_WindMap);
            
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;
            // -----------------------------------------------------------

            TEXTURE2D(_TerrainMap);
            SAMPLER(sampler_TerrainMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;

                float _WindMultiplier; // Réglage local

                float _MinScale;
                float _MaxScale;
                float _ColorVar;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float windMask : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float rnd : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. Récupération des données d'instance (Position + Random Seed)
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // 2. Rotation & Scale aléatoires
                float angle = rnd * 6.283185;
                float s, c;
                sincos(angle, s, c);
                float scale = lerp(_MinScale, _MaxScale, rnd);

                float3 posOS = input.positionOS.xyz * scale;
                
                // Rotation Y simple
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;

                float3 positionWS = instancePos + posOS;

                // --- 3. PHYSIQUE DU VENT GLOBALE ---
                // On utilise les variables globales définies par GlobalWindManager
                
                float time = _Time.y * _WindSpeed; 
                
                // Calcul UV identique à GlobalWindManager.cs : GetWindAtPosition
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                
                // Lecture de la texture de bruit globale
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // Application de la force
                float gust = noise * noise; // Accentuer les rafales
                float heightMask = input.uv.y * input.uv.y; // Le bas ne bouge pas, le haut bouge beaucoup

                // Combinaison : Force Globale * Réglage Local * Masque de hauteur * Rafale
                float combinedStrength = _GlobalWindStrength * _WindMultiplier;
                float totalPush = gust * combinedStrength * heightMask;

                // Displacement
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                
                // Petite compensation en Y pour simuler la courbure de l'herbe (arc)
                displacement.y -= totalPush * totalPush * 0.5;

                positionWS += displacement;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.windMask = gust * combinedStrength;
                output.positionWS = positionWS;
                output.rnd = rnd;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Terrain Blending
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 groundColor = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float4 rootCol = lerp(_BaseColor, groundColor, _BlendStrength);

                float heightGradient = pow(input.uv.y, 0.6);
                float4 finalColor = lerp(rootCol, _TipColor, heightGradient);

                // Variation de luminosité par brin
                float brightnessVar = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                finalColor *= brightnessVar;

                // Ombres
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                finalColor.rgb *= (mainLight.color * shadow);

                // Highlight des rafales de vent (Wind Visuals)
                float highlight = input.windMask * input.uv.y * _WaveOpacity; 

                // --- CORRECTION : Mode Additif ---
                // Au lieu de mélanger, on AJOUTE la couleur du vent comme de la lumière.
                // On multiplie par 2.0 ou 3.0 pour compenser le "noise * noise" du vertex shader.
                finalColor.rgb += _WaveColor.rgb * highlight * 4.0;

                // Fake Ambient Occlusion / Lumière basique
                finalColor.rgb += float3(0.05, 0.1, 0.05) * heightGradient;
                finalColor.rgb = min(finalColor.rgb, float3(0.95, 0.95, 0.95));

                return finalColor;
            }
            ENDHLSL
        }
    }
}
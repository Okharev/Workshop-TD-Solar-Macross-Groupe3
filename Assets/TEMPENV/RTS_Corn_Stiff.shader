Shader "Custom/RTS_Corn_VertexColor"
{
    Properties
    {
        [Header(Main Settings)]
        _TintColor ("Global Tint", Color) = (1, 1, 1, 1)
        _ColorVar ("Random Darken/Lighten", Range(0, 0.5)) = 0.2

        [Header(Terrain Blending)]
        // FIX: Par défaut "black" pour éviter que le bas soit blanc si aucune texture n'est mise
        _TerrainMap ("Terrain Color Map", 2D) = "black" {}
        _GroundColor ("Ground Default Color", Color) = (0.2, 0.15, 0.1, 1) // Marron foncé par défaut
        _TerrainSize ("Terrain Size", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Pos", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.3

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.0 
        _Stiffness ("Stem Stiffness", Range(0, 1)) = 0.6  // 0.8 pour Maïs (Dur), 0.2 pour Blé (Mou)
        _TipFlutter ("Leaf Shiver Strength", Float) = 0.1 // Tremblement rapide du sommet

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.8, 1.0, 0.8, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.15 

        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.85
        _MaxScale ("Max Scale", Float) = 1.3
        _YOffset ("Mesh Vertical Offset", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

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
            
            // --- GLOBAL WIND MANAGER VARIABLES ---
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;
            // -------------------------------------

            TEXTURE2D(_TerrainMap); SAMPLER(sampler_TerrainMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _GroundColor; // Fix couleur sol
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;
                float _WindMultiplier;
                float _Stiffness;   // Nouveau
                float _TipFlutter;  // Nouveau
                float _MinScale;
                float _MaxScale;
                float _ColorVar;
                float _YOffset;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
                uint instanceID : SV_InstanceID;
            };

            struct Varyings {
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
                
                // 1. SETUP INSTANCE
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // Scale & Rot
                float angle = rnd * 6.283185;
                float s, c;
                sincos(angle, s, c);
                float scale = lerp(_MinScale, _MaxScale, rnd);

                float3 posOS = input.positionOS.xyz * scale;
                
                // Rotation Y
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;
                
                float3 positionWS = instancePos + posOS;
                positionWS.y += _YOffset;

                // --- 2. CONFIGURATION PIVOT (Anti-Stretch) ---
                float3 pivotPos = instancePos + float3(0, _YOffset, 0);
                float origLength = length(positionWS - pivotPos);
                
                if (origLength < 0.001) {
                    // Si on est au pivot, on sort vite
                    output.positionCS = TransformWorldToHClip(positionWS);
                    output.uv = input.uv;
                    output.color = input.color;
                    output.windMask = 0;
                    output.positionWS = positionWS;
                    output.rnd = rnd;
                    return output;
                }

                // --- 3. WIND PHYSICS "NEXT-GEN" ---

                // A. Vent Global (Direction + Rafale)
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // B. Ambient Sway (Le mouvement "au repos")
                // Mouvement constant même sans rafale, désynchronisé par la position (rnd)
                float ambientFreq = _Time.y * 0.5 + rnd * 10.0; 
                float ambientSway = sin(ambientFreq) * 0.05;

                // C. High Freq Shiver (Tremblement des feuilles)
                // Vibration très rapide, uniquement sur les pointes
                float shiverFreq = _Time.y * 15.0 + rnd * 20.0;
                float shiver = sin(shiverFreq) * _TipFlutter * noise; // Activé par le vent

                // D. Combinaison des forces
                float heightMask = pow(input.uv.y, 2.0); // Courbure
                float tipMask = max(0, input.uv.y - 0.6); // Juste le sommet pour le shiver

                // Force Principale = (Rafale + Ambient) * ForceGlobale * Multiplicateur
                float mainWindForce = (noise + ambientSway) * _GlobalWindStrength * _WindMultiplier;
                
                // Application Rigidité (Stiffness) : Plus c'est rigide, moins la tige plie
                mainWindForce *= (1.0 - _Stiffness * 0.8); 

                float3 displacement = float3(0,0,0);
                // Déplacement principal (Tige)
                displacement.x = _WindDirection.x * mainWindForce * heightMask;
                displacement.z = _WindDirection.y * mainWindForce * heightMask;
                
                // Ajout du Shiver (Indépendant de la rigidité de la tige)
                displacement.x += shiver * tipMask;
                displacement.z += shiver * tipMask;

                float3 positionWithWind = positionWS + displacement;

                // --- 4. LENGTH LOCKING ---
                // On remet le sommet à sa distance d'origine du pivot
                float3 pivotToNew = positionWithWind - pivotPos;
                positionWithWind = pivotPos + normalize(pivotToNew) * origLength;

                positionWS = positionWithWind;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.windMask = noise * _GlobalWindStrength;
                output.positionWS = positionWS;
                output.rnd = rnd;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. COULEUR & GAMMA FIX ---
                float4 vertexColor = input.color;
                // Correction Gamma : Rend les couleurs vibrantes (plus de blanc délavé)
                vertexColor.rgb = pow(vertexColor.rgb, 2.2);

                float4 baseColor = vertexColor * _TintColor;

                // Variation Luminosité
                float brightness = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                baseColor.rgb *= brightness;

                // --- 2. TERRAIN BLENDING (DARK ROOTS FIX) ---
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 mapSample = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                
                // Si pas de texture (noir), on utilise _GroundColor (marron)
                float4 groundColor = mapSample + _GroundColor;
                groundColor = min(groundColor, float4(1,1,1,1));

                // Blend uniquement sur le bas (0.3)
                float blendFactor = max(0, (0.3 - input.uv.y) * _BlendStrength * 3.0);
                baseColor = lerp(baseColor, groundColor, blendFactor);

                // --- 3. LIGHTING ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                baseColor.rgb *= (mainLight.color * shadow);

                // --- 4. WIND HIGHLIGHTS ---
                float highlight = input.windMask * input.uv.y * _WaveOpacity * shadow;
                
                // Mode Additif Tint : On illumine en gardant la couleur de la plante
                float3 windTint = baseColor.rgb * _WaveColor.rgb * 3.0; 
                baseColor.rgb += windTint * highlight;

                return baseColor;
            }
            ENDHLSL
        }
    }
}
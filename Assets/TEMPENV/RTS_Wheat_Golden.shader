Shader "Custom/RTS_Wheat_VertexColor"
{
    Properties
    {
        [Header(Main Settings)]
        _TintColor ("Global Tint", Color) = (1, 1, 1, 1)
        
        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map (Top Down)", 2D) = "white" {}
        _TerrainSize ("Terrain Size (XZ)", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Position (XZ)", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.5

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.2 

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (1.0, 1.0, 0.8, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.3

        [Header(Variation)]
        _MinScale ("Min Scale Variance", Float) = 0.9
        _MaxScale ("Max Scale Variance", Float) = 1.1
        // --- NOUVEAU ---
        _ColorVar ("Random Darken/Lighten", Range(0, 0.5)) = 0.2
        _YOffset ("Mesh Vertical Offset", Float) = 0.0
        // ---------------
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

            // GLOBALS (Wind Manager)
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;

            TEXTURE2D(_TerrainMap); SAMPLER(sampler_TerrainMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;
                float _WindMultiplier;
                float _MinScale;
                float _MaxScale;
                // --- NOUVEAU ---
                float _ColorVar;
                float _YOffset;
                // ---------------
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
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // Scale & Rotation
                float angle = rnd * 6.283185;
                float s, c;
                sincos(angle, s, c);
                float scale = lerp(_MinScale, _MaxScale, rnd);

                float3 posOS = input.positionOS.xyz * scale;
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;
                
                float3 positionWS = instancePos + posOS;
                
                // On applique l'Offset Y
                positionWS.y += _YOffset;

                // --- 1. DEFINIR LE PIVOT ---
                // Le pivot suit l'instance + l'offset Y pour tourner correctement
                float3 pivotPos = instancePos + float3(0, _YOffset, 0);
                
                // Calculer la longueur d'origine du sommet par rapport au pivot
                float origLength = length(positionWS - pivotPos);

                // --- 2. WIND PHYSICS ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                float gust = noise * noise;
                float heightMask = input.uv.y * input.uv.y; // Courbure quadratique

                float totalPush = gust * _GlobalWindStrength * _WindMultiplier * heightMask;
                
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                
                // Note : On retire la compensation manuelle "displacement.y -= ..." 
                // car le Length Locking va gérer la descente Y automatiquement et parfaitement.

                float3 positionWithWind = positionWS + displacement;

                // --- 3. LENGTH PRESERVATION (ANTI-STRETCH) ---
                // Vecteur du Pivot vers la Nouvelle Position (étirée)
                float3 pivotToNew = positionWithWind - pivotPos;

                // On normalise ce vecteur et on le remet à la longueur d'origine
                // Si origLength est 0 (à la base), on évite la division par zéro
                if (origLength > 0.0001)
                {
                    positionWithWind = pivotPos + normalize(pivotToNew) * origLength;
                }
                
                positionWS = positionWithWind;
                // ---------------------------------------------

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color; 
                output.windMask = gust * _GlobalWindStrength;
                output.positionWS = positionWS;
                output.rnd = rnd;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Base Color = Vertex Color * Tint
                float4 baseColor = input.color * _TintColor;

                // --- NOUVEAU : VARIATION DE LUMINOSITÉ ALÉATOIRE ---
                // input.rnd est unique par brin (vient du buffer Compute)
                float brightness = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                baseColor.rgb *= brightness;
                // ---------------------------------------------------

                // 2. Terrain Blending
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 groundColor = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float blendFactor = (1.0 - input.uv.y) * _BlendStrength; 
                baseColor = lerp(baseColor, groundColor, blendFactor * 0.5);

                // 3. Lighting
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                baseColor.rgb *= (mainLight.color * shadow);

                // 4. Wind Highlights
                float highlight = input.windMask * input.uv.y * _WaveOpacity * shadow;
                baseColor = lerp(baseColor, _WaveColor, highlight);

                return baseColor;
            }
            ENDHLSL
        }
    }
}
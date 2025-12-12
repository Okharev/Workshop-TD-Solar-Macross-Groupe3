Shader "Custom/RTS_Corn_VertexColor"
{
    Properties
    {
        [Header(Main Settings)]
        _TintColor ("Global Tint", Color) = (1, 1, 1, 1)

        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map", 2D) = "white" {}
        _TerrainSize ("Terrain Size", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Pos", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.3

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 0.6 

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.8, 1.0, 0.8, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.15 

        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.85
        _MaxScale ("Max Scale", Float) = 1.3
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
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // Scale & Rot
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
                
                // Apply Offset Y
                positionWS.y += _YOffset;

                // --- 1. DEFINIR LE PIVOT ---
                float3 pivotPos = instancePos + float3(0, _YOffset, 0);
                float origLength = length(positionWS - pivotPos);

                // --- 2. WIND ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                
                float gust = noise; 
                float heightMask = input.uv.y * input.uv.y; 

                float totalPush = gust * _GlobalWindStrength * _WindMultiplier * heightMask;

                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                
                // Suppression de la compensation Y manuelle ici aussi
                
                float3 positionWithWind = positionWS + displacement;

                // --- 3. ANTI-STRETCH (Length Locking) ---
                float3 pivotToNew = positionWithWind - pivotPos;

                if (origLength > 0.0001)
                {
                    positionWithWind = pivotPos + normalize(pivotToNew) * origLength;
                }

                positionWS = positionWithWind;
                // ----------------------------------------

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
                float4 baseColor = input.color * _TintColor;

                // --- VARIATION LUMINOSITÉ ---
                float brightness = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                baseColor.rgb *= brightness;
                // ----------------------------

                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 groundColor = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float blendFactor = max(0, (0.5 - input.uv.y) * _BlendStrength * 2);
                baseColor = lerp(baseColor, groundColor, blendFactor);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                baseColor.rgb *= (mainLight.color * shadow);

                float highlight = input.windMask * input.uv.y * _WaveOpacity * shadow;
                baseColor.rgb += _WaveColor.rgb * highlight * 0.5;

                return baseColor;
            }
            ENDHLSL
        }
    }
}
Shader "Custom/RTS_Corn_AdvancedLit"
{
    Properties
    {
        [Header(Main Settings)]
        _TintColor ("Global Tint", Color) = (1, 1, 1, 1)
        _ColorVar ("Random Darken/Lighten", Range(0, 0.5)) = 0.2

        [Header(Lighting Hack)]
        // --- NOUVEAU : Force la normale vers le haut pour mieux capter la lumière ---
        _NormalCorrection ("Force Up Normal", Range(0, 1)) = 0.8

        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map", 2D) = "black" {}
        _GroundColor ("Ground Default Color", Color) = (0.2, 0.15, 0.1, 1)
        _TerrainSize ("Terrain Size", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Pos", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.3

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.0 
        _Stiffness ("Stem Stiffness", Range(0, 1)) = 0.6 
        _TipFlutter ("Leaf Shiver Strength", Float) = 0.1

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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

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
                float4 _GroundColor;
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;
                float _WindMultiplier;
                float _Stiffness;
                float _TipFlutter;
                float _MinScale;
                float _MaxScale;
                float _ColorVar;
                float _YOffset;
                // --- NOUVEAU ---
                float _NormalCorrection;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL; 
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR; 
                uint instanceID   : SV_InstanceID;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : TEXCOORD1;
                float windMask    : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS   : TEXCOORD5;
                float rnd         : TEXCOORD6;
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

                // --- 1. GESTION DES NORMALES (LE HACK) ---
                float3 rawNormal = input.normalOS;
                
                // Sécurité : Si pas de normale, on met Up
                if (length(rawNormal) < 0.01) rawNormal = float3(0, 1, 0);
                
                // Le mélange : On redresse la normale vers le haut selon _NormalCorrection
                float3 blendedNormal = lerp(rawNormal, float3(0, 1, 0), _NormalCorrection);
                blendedNormal = normalize(blendedNormal);

                // Rotation de la normale (pour suivre l'instance)
                float nxNew = blendedNormal.x * c - blendedNormal.z * s;
                float nzNew = blendedNormal.x * s + blendedNormal.z * c;
                float3 normalWS = float3(nxNew, blendedNormal.y, nzNew);

                // --- 2. POSITION ---
                float3 posOS = input.positionOS.xyz * scale;
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;

                float3 positionWS = instancePos + posOS;
                positionWS.y += _YOffset;

                // Pivot & Length Lock
                float3 pivotPos = instancePos + float3(0, _YOffset, 0);
                float origLength = length(positionWS - pivotPos);
                
                if (origLength < 0.001) {
                    output.positionCS = TransformWorldToHClip(positionWS);
                    output.uv = input.uv;
                    output.color = input.color;
                    output.windMask = 0;
                    output.positionWS = positionWS;
                    output.normalWS = normalWS;
                    output.rnd = rnd;
                    return output;
                }

                // Wind Physics
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                float ambientFreq = _Time.y * 0.5 + rnd * 10.0;
                float ambientSway = sin(ambientFreq) * 0.05;
                float shiverFreq = _Time.y * 15.0 + rnd * 20.0;
                float shiver = sin(shiverFreq) * _TipFlutter * noise;

                float heightMask = pow(input.uv.y, 2.0);
                float tipMask = max(0, input.uv.y - 0.6);

                float mainWindForce = (noise + ambientSway) * _GlobalWindStrength * _WindMultiplier;
                mainWindForce *= (1.0 - _Stiffness * 0.8);

                float3 displacement = float3(0,0,0);
                displacement.x = _WindDirection.x * mainWindForce * heightMask;
                displacement.z = _WindDirection.y * mainWindForce * heightMask;
                displacement.x += shiver * tipMask;
                displacement.z += shiver * tipMask;

                float3 positionWithWind = positionWS + displacement;
                float3 pivotToNew = positionWithWind - pivotPos;
                positionWithWind = pivotPos + normalize(pivotToNew) * origLength;

                positionWS = positionWithWind;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.windMask = noise * _GlobalWindStrength;
                output.positionWS = positionWS;
                output.normalWS = normalize(normalWS);
                output.rnd = rnd;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Base Color
                float4 vertexColor = input.color;
                vertexColor.rgb = pow(vertexColor.rgb, 2.2);
                float4 baseColor = vertexColor * _TintColor;

                float brightness = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                baseColor.rgb *= brightness;

                // Terrain Blend
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 mapSample = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float4 groundColor = mapSample + _GroundColor;
                groundColor = min(groundColor, float4(1,1,1,1));
                float blendFactor = max(0, (0.3 - input.uv.y) * _BlendStrength * 3.0);
                baseColor = lerp(baseColor, groundColor, blendFactor);

                // --- ECLAIRAGE AVANCE ---
                float3 N = normalize(input.normalWS);
                
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                float3 ambient = SampleSH(N);
                
                float NdotL = dot(N, mainLight.direction);
                float diffuse = max(0, NdotL * 0.6 + 0.4); 
                float3 finalLight = ambient + (mainLight.color * diffuse * mainLight.shadowAttenuation);

                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    float NdotL_add = dot(N, light.direction);
                    float diffuse_add = max(0, NdotL_add * 0.6 + 0.4);
                    finalLight += light.color * diffuse_add * light.distanceAttenuation * light.shadowAttenuation;
                }
                #endif

                baseColor.rgb *= finalLight;

                float highlight = input.windMask * input.uv.y * _WaveOpacity;
                float3 windTint = baseColor.rgb * _WaveColor.rgb * 3.0;
                baseColor.rgb += windTint * highlight;

                return baseColor;
            }
            ENDHLSL
        }
    }
}
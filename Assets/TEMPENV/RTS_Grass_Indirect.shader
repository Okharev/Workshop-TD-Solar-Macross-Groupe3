Shader "Custom/Grass_Indirect"
{
    Properties
    {
        [Header(Grass Colors)]
        _BaseColor ("Root Color (Shadow)", Color) = (0.1, 0.25, 0.1, 1)
        _TipColor ("Tip Color (Sun)", Color) = (0.5, 0.8, 0.1, 1)
        _ColorVar ("Blade Variation", Range(0, 0.5)) = 0.15

        [Header(Shape)]
        _BladeWidth ("Blade Width Multiplier", Range(0.5, 3.0)) = 1.0 
        _MinScale ("Min Height Scale", Float) = 0.8
        _MaxScale ("Max Height Scale", Float) = 1.2

        [Header(Specular)]
        _Gloss ("Glossiness", Float) = 32.0
        _SpecularIntensity ("Specular Intensity", Range(0, 1)) = 0.1

        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map (Top Down)", 2D) = "white" {}
        _TerrainSize ("Terrain Size (XZ)", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Position (XZ)", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.8

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.0

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.9, 1.0, 0.7, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.4
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
                float4 _BaseColor;
                float4 _TipColor;
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;
                float _WindMultiplier;
                float _MinScale;
                float _MaxScale;
                float _BladeWidth; 
                float _ColorVar;
                float _Gloss;
                float _SpecularIntensity;
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

                // --- SHAPE CONTROL ---
                float angle = rnd * 6.283185;
                float s, c;
                sincos(angle, s, c);
                
                float heightScale = lerp(_MinScale, _MaxScale, rnd);
                float3 posOS = input.positionOS.xyz * heightScale;
                
                // CONTROL WIDTH
                posOS.xz *= _BladeWidth; 

                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;

                float3 positionWS = instancePos + posOS;

                // --- WIND PHYSICS ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float ambientFreq = _Time.y * 2.0 + (instancePos.x + instancePos.z) * 5.0; 
                float ambientSway = sin(ambientFreq) * 0.05;
                float gust = noise * noise;
                float heightMask = input.uv.y * input.uv.y;
                float totalPush = (gust * _GlobalWindStrength + ambientSway) * _WindMultiplier * heightMask;
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                displacement.y -= totalPush * totalPush * 0.5;
                positionWS += displacement;

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
                // Terrain Blend
                float2 safeTerrainSize = max(_TerrainSize.xz, 0.001); 
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / safeTerrainSize;
                float4 groundColor = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float4 rootCol = lerp(_BaseColor, groundColor, _BlendStrength);

                float heightGradient = pow(saturate(input.uv.y), 0.6);
                float4 albedo = lerp(rootCol, _TipColor, heightGradient);
                
                // Color Variation
                albedo *= lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                
                // 1. SUGGESTION AO : Assombrir les racines pour l'ancrage au sol
                albedo.rgb *= smoothstep(-0.2, 0.6, input.uv.y);

                // Lighting Setup
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                float3 normalWS = float3(0, 1, 0);

                // Diffuse
                float NdotL = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
                float3 diffuse = albedo.rgb * mainLight.color * NdotL * shadow;

                // Ambient (Sky color)
                float3 ambient = SampleSH(normalWS) * albedo.rgb;

                // 2. SUGGESTION SPECULAR : Petit reflet brillant
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 halfVector = normalize(mainLight.direction + viewDir);
                float NdotH = saturate(dot(normalWS, halfVector));
                float specular = pow(NdotH, _Gloss) * _SpecularIntensity;
                float3 specularColor = specular * mainLight.color * shadow;

                // Wind Highlight
                float highlight = input.windMask * input.uv.y * _WaveOpacity;
                float3 windGusts = _WaveColor.rgb * mainLight.color * highlight * 2.0 * shadow;

                // Combine
                float3 finalRGB = diffuse + ambient + windGusts + specularColor;

                // Fog
                float fogFactor = ComputeFogFactor(input.positionCS.z);
                finalRGB = MixFog(finalRGB, fogFactor);

                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
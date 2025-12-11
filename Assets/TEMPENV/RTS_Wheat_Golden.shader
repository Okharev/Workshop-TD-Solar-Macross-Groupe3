Shader "Custom/Wheat_Indirect"
{
    Properties
    {
        [Header(Wheat Colors)]
        _BaseColor ("Root Color (Green/Brown)", Color) = (0.2, 0.25, 0.1, 1)
        _TipColor ("Head Color (Golden)", Color) = (0.9, 0.7, 0.2, 1)
        _SunTint ("Sun Highlight Strength", Range(0,1)) = 0.3

        [Header(Wind Physics)]
        _WindStrength ("Sway Strength", Float) = 1.5

        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.9
        _MaxScale ("Max Scale", Float) = 1.3
        _ColorVar ("Color Variation", Range(0, 0.5)) = 0.2

        [Header(Wind Physics)]
        _WindMultiplier ("Sway Responsiveness", Float) = 1.5

        _YOffset ("Mesh Vertical Offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
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
            TEXTURE2D(_WindMap);
            SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _SunTint;
                float _WindStrength;
                float _WindMultiplier;
                float _MinScale;
                float _MaxScale;
                float _ColorVar;
                float _YOffset;
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
                float3 positionWS : TEXCOORD3;
                float windGust : TEXCOORD4;
                float rnd : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // --- 1. DATA & INSTANCING ---
                // Get data from GPU buff
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // --- 2. VARIATION (Scale & Rotation) ---
                float scale = lerp(_MinScale, _MaxScale, rnd);
                float angle = rnd * 6.283185;
                float s, c;
                sincos(angle, s, c);

                // Initialisation de la position locale
                float3 posOS = input.positionOS.xyz;

                // Height adjustement for meshses with center pivot point
                //TODO Not optimized, needs good pivoted models
                posOS.y += _YOffset;

                // appply scale
                posOS *= scale;

                // apply rotation
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew;
                posOS.z = zNew;

                // --- 3. Global WIND PHYSICS ---
                float2 windDir = _WindDirection;
                if (length(windDir) == 0) windDir = float2(1, 0.5); // Sécurité

                float time = _Time.y * _WindSpeed;
                float2 windUV = instancePos.xz * _WindScale - (windDir * time);

                // a. Noise Texture wind
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // b. specific wheat wave pattern
                float wave = sin(time * 2.0 + instancePos.x + instancePos.z);

                // c. combine
                float combinedWind = noise + (wave * 0.2);

                // d. local + global force 
                float finalWindStrength = _GlobalWindStrength * _WindMultiplier;

                // e. apply bend, the higher, the bendier
                float bend = combinedWind * finalWindStrength * input.uv.y;

                float3 displacement = float3(windDir.x, 0, windDir.y) * bend;

                // compensate vertically
                displacement.y -= abs(bend) * 0.3;

                // final world pos
                float3 positionWS = instancePos + posOS + displacement;

                // --- 4. OUTPUT ---
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.positionWS = positionWS;
                output.windGust = noise;
                output.rnd = rnd;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. RANDOM VARIATION ---
                float brightnessVar = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);

                // --- 2. GRADIENT ---
                float heightGradient = pow(input.uv.y, 0.7);
                float4 baseCol = lerp(_BaseColor, _TipColor, heightGradient);

                float4 finalColor = baseCol * brightnessVar;

                // --- 3. LIGHTING & SHADOWS ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;

                // --- 4. CONTROLLED HIGHLIGHT ---
                float sunTouch = input.windGust * _SunTint * shadow * heightGradient;
                float3 sunColor = float3(0.9, 0.8, 0.2);
                finalColor.rgb = lerp(finalColor.rgb, sunColor, sunTouch * 0.5);

                // Apply Main Light + Shadow
                finalColor.rgb *= (mainLight.color * shadow);

                // --- 5. DEEP AMBIENT ---
                float3 ambient = float3(0.1, 0.1, 0.05) * smoothstep(0.0, 0.5, input.uv.y);
                finalColor.rgb += ambient;
                finalColor.rgb = min(finalColor.rgb, float3(0.95, 0.95, 0.95));

                return finalColor;
            }
            ENDHLSL
        }
    }
}
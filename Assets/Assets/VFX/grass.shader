Shader "Custom/RTS_Grass_Pro_Wind"
{
    Properties
    {
        [Header(Base Appearance)]
        _BaseColor ("Root Color (Shadow)", Color) = (0.1, 0.25, 0.1, 1)
        _TipColor ("Tip Color (Sun)", Color) = (0.5, 0.8, 0.1, 1)
        
        [Header(Wind Wave Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.9, 1.0, 0.7, 1) 
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.5

        [Header(Wind Physics)]
        _WindSpeed ("Wind Speed", Float) = 1.5
        // IMPACT: How far the grass pushes sideways
        _WindStrength ("Bending Strength", Float) = 0.8 
        // SIZE: Smaller number = BIGGER waves (Try 0.01 to 0.05)
        _WindScale ("Wave Frequency (Lower is Bigger)", Float) = 0.02
        
        [Header(Direction)]
        _WindDirX ("Wind Direction X", Range(-1, 1)) = 1.0
        _WindDirZ ("Wind Direction Z", Range(-1, 1)) = 0.4
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue"="Geometry" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float windMask : TEXCOORD1; // Passed to pixel shader for color
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TipColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _WaveColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaveOpacity)
                UNITY_DEFINE_INSTANCED_PROP(float, _WindSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _WindStrength)
                UNITY_DEFINE_INSTANCED_PROP(float, _WindScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _WindDirX)
                UNITY_DEFINE_INSTANCED_PROP(float, _WindDirZ)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            // Smooth Sine-like noise
            float N21(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float SmoothNoise(float2 worldPos, float scale) {
                float2 p = worldPos * scale;
                float2 ip = floor(p);
                float2 u = frac(p);
                // Smoother interpolation
                u = u * u * u * (u * (u * 6.0 - 15.0) + 10.0); 
                
                float res = lerp(
                    lerp(N21(ip), N21(ip + float2(1.0, 0.0)), u.x),
                    lerp(N21(ip + float2(0.0, 1.0)), N21(ip + float2(1.0, 1.0)), u.x), 
                    u.y);
                return res; // 0 to 1
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // --- FETCH ---
                float speed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WindSpeed);
                float strength = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WindStrength);
                float scale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WindScale);
                float dx = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WindDirX);
                float dz = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WindDirZ);
                float2 windDir = normalize(float2(dx, dz));

                // --- 1. THE BIG WAVE CALCULATION ---
                // We add Time to position to make it scroll
                float time = _Time.y * speed;
                float2 scrollPos = positionWS.xz - (windDir * time); 
                
                // Get Noise (0 to 1)
                float noise = SmoothNoise(scrollPos, scale);
                
                // Remap noise: We want distinct gusts, so let's sharpen the curve
                // This makes the "windy" parts stronger and the "calm" parts calmer
                float gust = pow(noise, 2.0); 

                // --- 2. BENDING PHYSICS ---
                // Bending is based on Height (UV.y) squared. 
                // Tips (1.0) move a lot, Roots (0.0) don't move at all.
                float heightMask = input.uv.y * input.uv.y;

                // Displacement: Move along Wind Direction
                // Base ambient movement (0.2) + Gust movement (0.8 * gust)
                float ambientSway = sin(time + positionWS.x * 0.5) * 0.1;
                float totalPush = (ambientSway + (gust * strength)) * heightMask;

                float3 displacement = float3(windDir.x * totalPush, 0, windDir.y * totalPush);

                // "Dip" logic: When we push X/Z, we must lower Y so the blade doesn't stretch
                // This creates the "bowing" effect
                displacement.y -= totalPush * totalPush * 0.5;

                // Apply
                positionWS += displacement;

                // --- OUTPUT ---
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.windMask = gust; // Pass the gust strength to pixel shader

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                float4 tipCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TipColor);
                float4 waveCol = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveColor);
                float waveOpacity = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaveOpacity);

                // 1. Base Gradient
                float4 finalColor = lerp(baseCol, tipCol, input.uv.y);

                // 2. Wind Gust Highlight
                // Only highlight the tips (input.uv.y) when the gust is strong (input.windMask)
                float highlight = input.windMask * input.uv.y * waveOpacity;
                
                // Blend smoothly
                finalColor = lerp(finalColor, waveCol, highlight);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
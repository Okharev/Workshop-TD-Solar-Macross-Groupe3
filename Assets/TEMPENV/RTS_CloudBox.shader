Shader "Custom/RTS_Cloud_Box"
{
    Properties
    {
        [Header(Main Colors)]
        _ColorA ("Sun Color (Low Density)", Color) = (1, 1, 1, 1)
        _ColorB ("Core Color (High Density)", Color) = (0.6, 0.6, 0.7, 1)

        [Header(Shape Density)]
        _NoiseMap ("Noise Texture", 2D) = "white" {}
        _Density ("Global Density", Range(0, 5)) = 1.0
        _StepCount ("Quality (Steps)", Range(8, 64)) = 32
        _NoiseScale ("Noise Scale", Float) = 1.0

        _Roundness ("Roundness", Range(0, 1)) = 1.0

        [Header(Wind)]
        _Speed ("Wind Speed", Vector) = (0.1, 0.05, 0, 0)

        [Header(Blending)]
        _EdgeFade ("Edge Softness", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _Speed;
                float _Density;
                float _NoiseScale;
                float _EdgeFade;
                float _Roundness;
                int _StepCount;
            CBUFFER_END

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            // Ray-Box Intersection
            float2 RayBoxIntersection(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
            {
                float3 t0 = (boxMin - rayOrigin) / rayDir;
                float3 t1 = (boxMax - rayOrigin) / rayDir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(min(tmax.x, tmax.y), tmax.z);
                return float2(dstA, dstB);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. SETUP RAY
                float3 cameraPosOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDir = normalize(input.positionOS - cameraPosOS);

                // 2. INTERSECT BOX (Optimization Bounds)
                float2 rayHit = RayBoxIntersection(cameraPosOS, rayDir, float3(-0.5, -0.5, -0.5),
                                  float3(0.5, 0.5, 0.5));

                float dstToBox = max(0, rayHit.x);
                float dstInsideBox = max(0, rayHit.y - dstToBox);

                if (rayHit.x > rayHit.y) discard;

                // 3. DEPTH CHECK
                float2 screenspaceUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = SampleSceneDepth(screenspaceUV);
                // Note: LinearEyeDepth might need adjustment for Object Space if camera is close

                // 4. RAYMARCH
                float totalDensity = 0;
                float stepSize = dstInsideBox / float(_StepCount);
                float3 currentPos = cameraPosOS + rayDir * dstToBox;

                // Dither/Jitter
                float jitter = frac(sin(dot(screenspaceUV, float2(12.9898, 78.233))) * 43758.5453);
                currentPos += rayDir * stepSize * jitter;

                float3 windOffset = _Time.y * _Speed.xyz;

                for (int i = 0; i < _StepCount; i++)
                {
                    if (totalDensity >= 1.0) break;

                    // Sample Density
                    float3 uvw = (currentPos * _NoiseScale) + windOffset;
                    float n1 = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uvw.xy, 0).r;
                    float n2 = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uvw.xz + float2(0.5, 0.5), 0).r;
                    float noise = (n1 * n2) * 2.0;

                    // --- SHAPE MASKING LOGIC ---
                    // Box Distance (Max axis distance)
                    float3 dAbs = abs(currentPos) * 2.0;
                    float distBox = max(dAbs.x, max(dAbs.y, dAbs.z));

                    // Sphere Distance (Distance from center)
                    float distSphere = length(currentPos) * 2.0;

                    // Blend shapes based on _Roundness slider
                    float distShape = lerp(distBox, distSphere, _Roundness);

                    // Fade out as we reach the edge (1.0)
                    float borderMask = smoothstep(1.0, 1.0 - _EdgeFade, distShape);

                    float localDensity = noise * borderMask * _Density;
                    totalDensity += localDensity * stepSize * 4.0;

                    currentPos += rayDir * stepSize;
                }

                float finalAlpha = saturate(totalDensity);
                float4 finalColor = lerp(_ColorA, _ColorB, finalAlpha * 0.8);
                finalColor.a = finalAlpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
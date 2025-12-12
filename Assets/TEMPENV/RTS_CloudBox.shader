Shader "Custom/URP/StylizedFluffyCloud_Ultimate"
{
    Properties
    {
        [Header(Shape Manipulation)]
        _FlattenY ("Flatten Height", Range(0.1, 1.0)) = 0.6
        _ShapeScale ("Shape Noise Scale", Range(0.1, 5)) = 0.4
        _ShapeDistortion ("Shape Distortion", Range(0, 5)) = 1.0
        _ShapeSpeed ("Shape Speed", Vector) = (0.2, 0.1, 0.05, 0) // Faster default

        [Header(Fluff Details)]
        _Displacement ("Fluff Strength", Range(0, 2)) = 0.6
        _NoiseScale ("Fluff Scale", Range(0.1, 10)) = 3.0
        _NoiseSpeed ("Fluff Speed", Vector) = (0.5, 0.8, 0.2, 0) // Faster default
        _Layers ("Noise Layers", Range(1, 4)) = 3
        
        [Header(Lighting  Color)]
        _BaseColor ("Sunlit Top Color", Color) = (1, 1, 1, 1)
        _BottomColor ("Dense Bottom Color", Color) = (0.7, 0.7, 0.8, 1) // NEW
        _ShadowColor ("Self-Shadow Color", Color) = (0.4, 0.4, 0.65, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = -0.1
        _ShadowSoftness ("Shadow Softness", Range(0.01, 1)) = 0.2
        _NormalStrength ("Bump Sharpness", Range(0, 5)) = 2.0
        
        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.1, 10)) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        HLSLINCLUDE
        // --- SHARED FUNCTIONS FOR BOTH PASSES (LIT AND SHADOW) ---
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BottomColor;
            float4 _ShadowColor;
            float4 _RimColor;
            float4 _NoiseSpeed;
            float4 _ShapeSpeed;
            float _FlattenY;
            float _Displacement;
            float _NoiseScale;
            float _ShapeScale;
            float _ShapeDistortion;
            float _Layers;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float _RimPower;
            float _NormalStrength;
        CBUFFER_END

        float3 hash33(float3 p) {
            p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                       dot(p, float3(269.5, 183.3, 246.1)),
                       dot(p, float3(113.5, 271.9, 124.6)));
            return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
        }

        float noise(float3 p) {
            float3 i = floor(p);
            float3 f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(dot(hash33(i+float3(0,0,0)),f-float3(0,0,0)),dot(hash33(i+float3(1,0,0)),f-float3(1,0,0)),u.x),
                        lerp(dot(hash33(i+float3(0,1,0)),f-float3(0,1,0)),dot(hash33(i+float3(1,1,0)),f-float3(1,1,0)),u.x),u.y),
                   lerp(lerp(dot(hash33(i+float3(0,0,1)),f-float3(0,0,1)),dot(hash33(i+float3(1,0,1)),f-float3(1,0,1)),u.x),
                        lerp(dot(hash33(i+float3(0,1,1)),f-float3(0,1,1)),dot(hash33(i+float3(1,1,1)),f-float3(1,1,1)),u.x),u.y),u.z);
        }

        float GetTotalDisplacement(float3 pos) {
            float3 shapePos = (pos * _ShapeScale) + (_Time.y * _ShapeSpeed.xyz);
            float shapeNoise = noise(shapePos);
            
            float fluffTotal = 0.0;
            float amplitude = 0.5;
            float frequency = 1.0;
            float3 fluffPos = (pos * _NoiseScale) + (_Time.y * _NoiseSpeed.xyz);

            for(int i = 0; i < 3; i++) {
                fluffTotal += noise(fluffPos * frequency) * amplitude;
                amplitude *= 0.5;
                frequency *= 2.0;
            }
            float fluffMask = smoothstep(0.0, 0.8, fluffTotal);
            return (shapeNoise * _ShapeDistortion) + (fluffMask * _Displacement);
        }
        ENDHLSL

        // --- PASS 1: MAIN RENDERING ---
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 originalPosOS : TEXCOORD3; // Used for vertical gradient
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                output.originalPosOS = posOS; // Store original for coloring

                // 1. FLATTEN LOGIC (Squash height before noise so puffballs stay round)
                posOS.y *= _FlattenY; 

                // 2. DISPLACEMENT
                float totalH = GetTotalDisplacement(posOS); // Use squashed pos for noise sampling
                float3 displacedPos = posOS + (input.normalOS * totalH);

                // 3. RECALCULATE NORMALS
                float epsilon = 0.05;
                float h_x = GetTotalDisplacement(posOS + float3(epsilon, 0, 0));
                float h_y = GetTotalDisplacement(posOS + float3(0, epsilon, 0));
                float h_z = GetTotalDisplacement(posOS + float3(0, 0, epsilon));
                float3 gradient = float3(h_x - totalH, h_y - totalH, h_z - totalH);
                float3 rawNewNormal = input.normalOS - (gradient * _NormalStrength * 5.0);

                // Adjust normal scale due to flattening
                rawNewNormal.y /= _FlattenY; 
                
                output.normalWS = TransformObjectToWorldNormal(normalize(rawNewNormal));
                output.positionWS = TransformObjectToWorld(displacedPos);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);

                float NdotL = dot(normal, lightDir);
                float lightIntensity = smoothstep(_ShadowThreshold, _ShadowThreshold + _ShadowSoftness, NdotL);
                
                // --- IMPROVEMENT: Vertical Gradient ---
                // Interpolate between Bottom Color and Top Color based on object height
                float heightGradient = saturate(input.originalPosOS.y + 0.5); 
                float3 albedo = lerp(_BottomColor.rgb, _BaseColor.rgb, heightGradient);
                
                float3 finalColor = lerp(_ShadowColor.rgb, albedo, lightIntensity);

                // Rim Light
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                fresnel = pow(fresnel, _RimPower);
                finalColor += _RimColor.rgb * fresnel;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // --- PASS 2: SHADOW CASTER (Makes the cloud cast shadows) ---
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                
                // MATCHING FLATTEN LOGIC
                posOS.y *= _FlattenY; 

                // MATCHING DISPLACEMENT LOGIC
                float totalH = GetTotalDisplacement(posOS);
                float3 displacedPos = posOS + (input.normalOS * totalH);

                float3 positionWS = TransformObjectToWorld(displacedPos);
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // Required for shadow bias to work correctly
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                return 0;
            }
            ENDHLSL
        }
    }
}
Shader "Custom/Flower_Indirect"
{
    Properties
    {
        [Header(Colors)]
        _StemColor ("Stem Color (Bottom)", Color) = (0.1, 0.3, 0.1, 1)
        _TipColorA ("Petal Color Main", Color) = (0.8, 0.1, 0.1, 1) // Example: Red
        _TipColorB ("Petal Color Variation", Color) = (0.9, 0.4, 0.1, 1) // Example: Orange

        [Header(Visuals)]
        // Defines where the stem ends and petals begin (0.0 = bottom, 1.0 = top)
        _PetalStart ("Petal Height Start", Range(0, 1)) = 0.6

        [Header(Wind Physics)]
        _WindStrength ("Bending Strength", Float) = 0.5

        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.8
        _MaxScale ("Max Scale", Float) = 1.2
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
                float4 _StemColor;
                float4 _TipColorA;
                float4 _TipColorB;
                float _PetalStart;
                float _WindStrength;
                float _MinScale;
                float _MaxScale;
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
                float rnd : TEXCOORD5; // Passing Random ID to Fragment
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. DATA
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w;

                // 2. VARIATION
                float scale = lerp(_MinScale, _MaxScale, rnd);
                float3 posOS = input.positionOS.xyz * scale;

                // Random Y Rotation
                float angleY = rnd * 6.283185;
                float sy, cy;
                sincos(angleY, sy, cy);
                float xNew = posOS.x * cy - posOS.z * sy;
                float zNew = posOS.x * sy + posOS.z * cy;
                posOS.x = xNew;
                posOS.z = zNew;

                // 3. WIND PHYSICS (Rigid Stem Logic)
                float2 windDir = _WindDirection;
                if (length(windDir) == 0) windDir = float2(1, 1);

                float time = _Time.y * _WindSpeed;
                float2 windUV = instancePos.xz * _WindScale - (windDir * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // Bend Calculation
                float bendAngle = noise * _WindStrength * _GlobalWindStrength * 0.5;
                float h = posOS.y;

                // Rigid movement logic (prevents stretching)
                float3 bendOffset = float3(windDir.x, 0, windDir.y) * (bendAngle * h);
                float verticalDrop = abs(bendAngle) * h * 0.5;

                posOS += bendOffset;
                posOS.y -= verticalDrop;

                float3 positionWS = instancePos + posOS;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.rnd = rnd; // Pass to Pixel Shader

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. DETERMINE PETAL COLOR ---
                // Randomized color between the 2 colors given
                float4 petalColor = lerp(_TipColorA, _TipColorB, input.rnd);

                // --- 2. MIX STEM AND PETAL ---
                // smoothstep to blend stim for bud
                float petalMask = smoothstep(_PetalStart, _PetalStart + 0.2, input.uv.y);

                float4 finalColor = lerp(_StemColor, petalColor, petalMask);

                // --- 3. LIGHTING ---
                Light mainLight = GetMainLight();

                // Simple lighting multiplication
                finalColor.rgb *= mainLight.color;

                // Add a little ambient light so the flowers aren't pitch black in shadow
                finalColor.rgb += float3(0.1, 0.1, 0.1);

                finalColor.rgb = min(finalColor.rgb, float3(0.95, 0.95, 0.95));
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
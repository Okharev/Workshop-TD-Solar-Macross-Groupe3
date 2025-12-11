Shader "Custom/RTS_Corn_Stiff"
{
    Properties
    {
        [Header(Corn Colors)]
        _BaseColor ("Stalk Color", Color) = (0.1, 0.3, 0.1, 1)
        _TipColor ("Dry Leaf Color", Color) = (0.6, 0.6, 0.2, 1)
        
        [Header(Wind Physics)]
        _WindStrength ("Bending Strength", Float) = 0.4 
        _Turbulence ("Leaf Flutter", Float) = 0.1 
        
        [Header(Dimensions)]
        _MinScale ("Min Height", Float) = 0.9
        _MaxScale ("Max Height", Float) = 1.5 
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

            // UPDATE: Changed to float4 for baked seed
            StructuredBuffer<float4> _VisibleInstances;
            
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection; 

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _WindStrength;
                float _Turbulence;
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
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. DATA RETRIEVAL
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w; // Baked Random Seed

                // 2. VARIATION
                float scale = lerp(_MinScale, _MaxScale, rnd);
                
                // Setup position object
                float3 posOS = input.positionOS.xyz;
                posOS.y *= scale; 
                posOS.xz *= (scale * 0.8);
                
                // Random Rotation
                float angle = rnd * 6.283185;
                float s, c; sincos(angle, s, c);
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew; posOS.z = zNew;

                // 3. WIND PHYSICS (Stiff Corn)
                float2 windDir = _WindDirection; 

                float time = _Time.y * _WindSpeed;
                float2 windUV = instancePos.xz * _WindScale - (windDir * time);
                
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // Stiffness: High power (pow 3) keeps the base very rigid
                float stiffnessMask = input.uv.y * input.uv.y * input.uv.y; 
                
                float bend = noise * _WindStrength * stiffnessMask;

                // Turbulence (Leaf flutter)
                float flutter = sin(time * 15.0 + posOS.x * 10.0) * _Turbulence * input.uv.y;

                // Combined movement
                float3 windMove = float3(windDir.x, 0, windDir.y) * (bend + flutter);
                
                float3 positionWS = instancePos + posOS + windMove;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.positionWS = positionWS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
            
                // Color Gradient
                float4 col = lerp(_BaseColor, _TipColor, smoothstep(0.4, 1.0, input.uv.y));

                // Shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                
                col.rgb *= (mainLight.color * shadow);

                // Fake AO at roots
                col.rgb *= smoothstep(0.0, 0.2, input.uv.y) + 0.2;

                return col;
            }
            ENDHLSL
        }
    }
}
Shader "Custom/RTS_Corn_Stiff_Global"
{
    Properties
    {
        [Header(Corn Colors)]
        _BaseColor ("Stalk Color (Green)", Color) = (0.1, 0.3, 0.1, 1)
        _TipColor ("Dry Leaf Color (Yellow)", Color) = (0.6, 0.6, 0.2, 1)
        _SunTint ("Sun Highlight Strength", Range(0,1)) = 0.4 
        
        [Header(Wind Physics)]
        _WindMultiplier ("Stiffness Resistance", Float) = 0.4 // Plus bas = plus rigide
        _Turbulence ("Leaf Flutter Speed", Float) = 1.0 
    
        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.9
        _MaxScale ("Max Scale", Float) = 1.5 
        _ColorVar ("Random Darken/Lighten", Range(0, 0.5)) = 0.2
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            StructuredBuffer<float4> _VisibleInstances;
            
            // --- GLOBALSS ---
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection; 
            float _GlobalWindStrength; 

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _SunTint;
                float _WindMultiplier;
                float _Turbulence;
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
                
                // 1. DATA RETRIEVAL
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w; 

                // 2. VARIATION & OFFSET
                float scale = lerp(_MinScale, _MaxScale, rnd);
                
                // Rotation
                float angle = rnd * 6.283185;
                float s, c; sincos(angle, s, c);

                float3 posOS = input.positionOS.xyz;

                // Application Offset Y 
                posOS.y += _YOffset;

                // Application Scale
                posOS *= scale;
                posOS.xz *= 0.9; 
                
                // Application Rotation
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew; posOS.z = zNew;

                // 3. WIND PHYSICS (Logic Corn Stiff)
                float2 windDir = _WindDirection;
                if(length(windDir) == 0) windDir = float2(1, 0.5);

                float time = _Time.y * _WindSpeed;
                float2 windUV = instancePos.xz * _WindScale - (windDir * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                // combine forces
                float totalWindStrength = _GlobalWindStrength * _WindMultiplier;

                // A. Rigidité (Stiffness)
                float stiffnessMask = pow(input.uv.y, 3.0);
                
                // B. Wind turbulance
                float flutter = sin(time * 15.0 + posOS.x * 10.0) * (_Turbulence * 0.1);
                flutter *= totalWindStrength * input.uv.y;

                // C. movements
                float bend = noise * totalWindStrength * stiffnessMask;
                
                // D. Application
                float3 displacement = float3(windDir.x, 0, windDir.y) * (bend + flutter);
                
                // Compensate vertically to avoid strectching
                displacement.y -= abs(bend) * 0.2; 

                float3 positionWS = instancePos + posOS + displacement;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color; // Vertex color
                output.positionWS = positionWS;
                output.windGust = noise; // noise for light efect
                output.rnd = rnd;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. Variations ---
                float brightnessVar = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                
                // --- 2. base color ---
                // Green to Yellow bottm up
                float4 baseCol = lerp(_BaseColor, _TipColor, smoothstep(0.3, 1.0, input.uv.y));
                
                // Application of vertex color and variants
                baseCol *= input.color * brightnessVar;

                // --- 3. Shadow and light ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                
                // --- 4. THE "FLAIR" ---
                float sunTouch = input.windGust * _SunTint * shadow * input.uv.y;
                float3 sunColor = float3(1.0, 0.9, 0.4);
                
                // Mix
                float4 finalColor = baseCol;
                finalColor.rgb = lerp(finalColor.rgb, sunColor, sunTouch * 0.6);

                // Main light
                finalColor.rgb *= mainLight.color * shadow;

                // --- 5. AMBIENT & AO ---
                float ao = smoothstep(0.0, 0.3, input.uv.y) * 0.5 + 0.5;
                finalColor.rgb *= ao;
                
                // avoid total black at bottom
                finalColor.rgb += float3(0.05, 0.1, 0.05) * 0.2;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
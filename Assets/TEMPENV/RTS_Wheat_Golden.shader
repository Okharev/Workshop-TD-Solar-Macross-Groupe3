Shader "Custom/RTS_Wheat_Golden"
{
    Properties
    {
        [Header(Wheat Colors)]
        // DARKER DEFAULT: I darkened the root color to create better contrast from the start
        _BaseColor ("Root Color (Green/Brown)", Color) = (0.2, 0.25, 0.1, 1)
        _TipColor ("Head Color (Golden)", Color) = (0.9, 0.7, 0.2, 1)
        _SunTint ("Sun Highlight Strength", Range(0,1)) = 0.3 // Reduced from 0.5

        [Header(Wind Physics)]
        _WindStrength ("Sway Strength", Float) = 1.5 
        
        [Header(Variation)]
        _MinScale ("Min Scale", Float) = 0.9
        _MaxScale ("Max Scale", Float) = 1.3
        
        // NEW: Controls how much random color difference exists between stalks
        _ColorVar ("Color Variation", Range(0, 0.5)) = 0.2 
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _SunTint;
                float _WindStrength;
                float _MinScale;
                float _MaxScale;
                float _ColorVar; // New Property
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
                float rnd : TEXCOORD5; // Passing random seed to Pixel Shader
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
                float angle = rnd * 6.283185;
                float s, c; sincos(angle, s, c);
                
                float3 posOS = input.positionOS.xyz * scale;
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew; posOS.z = zNew;
                
                // 3. WIND PHYSICS
                float2 windDir = _WindDirection;
                if(length(windDir) == 0) windDir = float2(1, 0.5);

                float time = _Time.y * _WindSpeed;
                float2 windUV = instancePos.xz * _WindScale - (windDir * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;

                float wave = sin(time * 2.0 + instancePos.x + instancePos.z);
                float combinedWind = noise + (wave * 0.2); 
                float bend = combinedWind * _WindStrength * input.uv.y;
                
                float3 displacement = float3(windDir.x, 0, windDir.y) * bend;
                displacement.y -= abs(bend) * 0.3;
                
                float3 positionWS = instancePos + posOS + displacement;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.positionWS = positionWS;
                output.windGust = noise; 
                output.rnd = rnd; // Pass to Frag

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. RANDOM VARIATION (Crucial for visibility) ---
                // We darken or lighten the entire stalk based on its random ID.
                // This breaks the "solid wall of color" effect.
                float brightnessVar = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                
                // --- 2. IMPROVED GRADIENT ---
                // We use pow() to push the dark root color higher up the stalk.
                // This creates better "fake shadows" between stalks.
                float heightGradient = pow(input.uv.y, 0.7); 
                float4 baseCol = lerp(_BaseColor, _TipColor, heightGradient);
                
                // Apply the random variation
                float4 finalColor = baseCol * brightnessVar;

                // --- 3. LIGHTING & SHADOWS ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;

                // --- 4. CONTROLLED HIGHLIGHT ---
                // Instead of adding pure yellow (which washes out detail), 
                // we modulate the brightness.
                float sunTouch = input.windGust * _SunTint * shadow * heightGradient;
                
                // Add highlight but keep it grounded
                float3 sunColor = float3(0.9, 0.8, 0.2); // Explicit golden color
                finalColor.rgb = lerp(finalColor.rgb, sunColor, sunTouch * 0.5); // Blend towards gold

                // Apply Main Light + Shadow
                finalColor.rgb *= (mainLight.color * shadow);
                
                // --- 5. DEEP AMBIENT ---
                // We add very little ambient light to the roots (darker roots = better separation)
                float3 ambient = float3(0.1, 0.1, 0.05) * smoothstep(0.0, 0.5, input.uv.y); 
                finalColor.rgb += ambient;
                finalColor.rgb = min(finalColor.rgb, float3(0.95, 0.95, 0.95));
                return finalColor;
            }
            ENDHLSL
        }
    }
}
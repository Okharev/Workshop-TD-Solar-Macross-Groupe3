Shader "Custom/RTS_Grass_Indirect"
{
    Properties
    {
        [Header(Grass Colors)]
        _BaseColor ("Root Color (Shadow)", Color) = (0.1, 0.25, 0.1, 1)
        _TipColor ("Tip Color (Sun)", Color) = (0.5, 0.8, 0.1, 1)
        
        // NEW: Micro-variation intensity
        _ColorVar ("Blade Variation", Range(0, 0.5)) = 0.15 

        [Header(Macro Variation)]
        _PatchColor ("Dry/Light Patch Color", Color) = (0.7, 0.7, 0.3, 1)
        _PatchScale ("Patch Scale (0.002 = 500m)", Float) = 0.002 
        _PatchStrength ("Patch Opacity", Range(0,1)) = 0.5
        _PatchNoiseMap ("Patch Noise Texture", 2D) = "white" {}

        [Header(Wind Physics)]
        _WindStrength ("Bending Strength", Range(0.0, 5.0)) = 1.0
        
        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.9, 1.0, 0.7, 1) 
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.4
        
        [Header(Blade Variation)]
        _MinScale ("Min Scale Variance", Float) = 0.8
        _MaxScale ("Max Scale Variance", Float) = 1.2
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
            
            TEXTURE2D(_PatchNoiseMap); SAMPLER(sampler_PatchNoiseMap);
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection; 

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _PatchColor;
                float4 _WaveColor;
                
                float _PatchScale;
                float _PatchStrength;
                float _WaveOpacity;
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
                float windMask : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float patchFactor : TEXCOORD5; 
                float rnd : TEXCOORD6; // Passing Random Seed to Frag
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. DATA
                float4 instanceData = _VisibleInstances[input.instanceID];
                float3 instancePos = instanceData.xyz;
                float rnd = instanceData.w; 

                // 2. ROTATION & SCALE
                float angle = rnd * 6.283185; 
                float s, c; sincos(angle, s, c);
                float scale = lerp(_MinScale, _MaxScale, rnd);
                
                float3 posOS = input.positionOS.xyz * scale;
                float xNew = posOS.x * c - posOS.z * s;
                float zNew = posOS.x * s + posOS.z * c;
                posOS.x = xNew; posOS.z = zNew;
                
                float3 positionWS = instancePos + posOS;

                // 3. WIND PHYSICS
                float2 windDir = _WindDirection; 
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (windDir * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                
                float gust = noise * noise; 
                float heightMask = input.uv.y * input.uv.y;
                float totalPush = gust * _WindStrength * heightMask;

                float3 displacement = float3(windDir.x * totalPush, 0, windDir.y * totalPush);
                displacement.y -= totalPush * totalPush * 0.5;
                
                positionWS += displacement;

                // 4. MACRO PATCH
                float2 patchUV = positionWS.xz * _PatchScale;
                float patchNoise = SAMPLE_TEXTURE2D_LOD(_PatchNoiseMap, sampler_PatchNoiseMap, patchUV, 0).r;
                output.patchFactor = smoothstep(0.3, 0.7, patchNoise);

                // 5. OUTPUT
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.windMask = gust; 
                output.positionWS = positionWS;
                output.rnd = rnd; // Pass seed to Frag for color variation
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- 1. BASE COLOR with DEEP ROOTS ---
                // We use pow() to keep the root color low and the tip color high.
                // This creates a fake "Ambient Occlusion" at the bottom.
                float heightGradient = pow(input.uv.y, 0.6); 
                float4 finalColor = lerp(_BaseColor, _TipColor, heightGradient);

                // --- 2. MACRO PATCH VARIATION ---
                // Apply the "Dry/Dead" patches calculated in Vertex Shader
                finalColor = lerp(finalColor, _PatchColor, input.patchFactor * _PatchStrength);

                // --- 3. MICRO BLADE VARIATION (New) ---
                // Randomly darken or lighten individual blades using the seed
                float brightnessVar = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                finalColor *= brightnessVar;

                // --- 4. LIGHTING ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                finalColor.rgb *= (mainLight.color * shadow);

                // --- 5. WIND HIGHLIGHT ---
                float highlight = input.windMask * input.uv.y * _WaveOpacity * shadow;
                finalColor = lerp(finalColor, _WaveColor, highlight);

                // --- 6. AMBIENT ---
                // Only add ambient light to the tips, keep roots dark!
                finalColor.rgb += float3(0.05, 0.1, 0.05) * heightGradient;
                finalColor.rgb = min(finalColor.rgb, float3(0.95, 0.95, 0.95));
                return finalColor;
            }
            ENDHLSL
        }
    }
}
Shader "Economy/EnergyProjector_GreyWorld"
{
    Properties
    {
        [Header(Baking Data)]
        _HeatmapTex ("Baked Heatmap", 2D) = "black" {}
        // X = Map Width, Y = Map Length, Z = OffsetX, W = OffsetZ
        _MapCoords ("Map Coords", Vector) = (100, 100, 0, 0) 

        [Header(Visuals)]
        _ColorLow ("Color Low", Color) = (0,0,1,0.5)
        _ColorMid ("Color Mid", Color) = (1,1,0,0.5)
        _ColorHigh ("Color High", Color) = (1,0,0,0.5)
        
        _MaxEnergy ("Max Energy Ref", Float) = 500.0
        
        [Header(Greyscale Settings)]
        _GreyDarkness ("Greyscale Darkness", Range(0, 1)) = 0.5
        _EnergyBrightness ("Energy Glow Intensity", Range(1, 3)) = 1.5
    }
    SubShader
    {
        // --- CORRECTION ICI ---
        // On dessine juste avant (Transparent-1) les objets transparents standards.
        // Ainsi, les particules se dessineront PAR-DESSUS le sol gris.
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Blend One Zero
        ZWrite Off
        Cull Front 
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            TEXTURE2D(_HeatmapTex);
            SAMPLER(sampler_HeatmapTex);
            
            float4 _MapCoords; 
            float _MaxEnergy;
            half4 _ColorLow;
            half4 _ColorMid;
            half4 _ColorHigh;
            
            float _GreyDarkness;
            float _EnergyBrightness;

            v2f vert (appdata_t v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = vertexInput.positionCS;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    if(rawDepth < 0.0001) discard; 
                #else
                    if(rawDepth > 0.9999) discard;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                half3 originalSceneColor = SampleSceneColor(screenUV);

                float luminance = dot(originalSceneColor, float3(0.2126, 0.7152, 0.0722));
                half3 greyScene = luminance.xxx * _GreyDarkness;

                float2 mapUV = (worldPos.xz - _MapCoords.zw) / _MapCoords.xy;
                mapUV += 0.5;

                float energy = 0;
                if(mapUV.x >= 0 && mapUV.x <= 1 && mapUV.y >= 0 && mapUV.y <= 1)
                {
                    energy = SAMPLE_TEXTURE2D(_HeatmapTex, sampler_HeatmapTex, mapUV).r;
                }

                float t = saturate(energy / _MaxEnergy);
                
                half3 heatColor = lerp(_ColorLow.rgb, _ColorMid.rgb, t * 2.0);
                if(t > 0.5) heatColor = lerp(_ColorMid.rgb, _ColorHigh.rgb, (t - 0.5) * 2.0);

                half3 energizedScene = (luminance * heatColor) * _EnergyBrightness;
                
                float mask = smoothstep(0.01, 0.1, energy);

                half3 finalRGB = lerp(greyScene, energizedScene, mask);

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}
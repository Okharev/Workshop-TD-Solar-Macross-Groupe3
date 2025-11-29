Shader "Economy/EnergyProjector_URP"
{
    Properties
    {
        _ColorLow ("Color Low (Low Current)", Color) = (0,0,1,0.5)
        _ColorMid ("Color Mid (Medium Current)", Color) = (1,1,0,0.5)
        _ColorHigh ("Color High (Strong Current)", Color) = (1,0,0,0.5)
        _MaxEnergy ("Max Energy Reference", Float) = 500.0
        _GlobalAlpha ("Global Transparency", Range(0,1)) = 0.6
    }
    SubShader
    {
        // Transparent Queue, but typically we render AFTER opaque geometry 
        // to paint on top of it.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // REQUIRED for Depth Sampling
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            // --- DATA ARRAYS ---
            float4 _GenData[128]; 
            int _GenCount;
            
            float _MaxEnergy;
            float _GlobalAlpha;
            half4 _ColorLow;
            half4 _ColorMid;
            half4 _ColorHigh;

            v2f vert (appdata_t v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.vertex = vertexInput.positionCS;
                
                // Calculate Screen Position for Depth Sampling
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. Calculate Screen UVs
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // 2. Sample the Depth Buffer (The geometry behind this quad)
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(screenUV);
                #else
                    // Adjust for OpenGL platforms if necessary
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(screenUV));
                #endif

                // 3. Reconstruct World Position from Depth
                // This gives us the exact position of the rock/grass/building at this pixel
                float3 worldPos = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                // 4. Run Energy Logic on that World Position
                float totalEnergy = 0;

                for(int k = 0; k < _GenCount; k++)
                {
                    float2 sourcePos = _GenData[k].xy;
                    float radius = _GenData[k].z;
                    float energy = _GenData[k].w;

                    // Note: worldPos.xz handles the terrain height automatically
                    float dist = distance(worldPos.xz, sourcePos);
                    
                    // Hard Zone Logic (Additive)
                    float inside = step(dist, radius);
                    totalEnergy += inside * energy;
                }

                // 5. Coloring
                if (totalEnergy <= 0.1) discard;

                float t = saturate(totalEnergy / _MaxEnergy);
                half4 col;

                if (t < 0.5) col = lerp(_ColorLow, _ColorMid, t * 2.0);
                else col = lerp(_ColorMid, _ColorHigh, (t - 0.5) * 2.0);

                col.a *= _GlobalAlpha;

                // Optional: Fade out if the camera is too close or far?
                // For now, simple return.
                return col;
            }
            ENDHLSL
        }
    }
}
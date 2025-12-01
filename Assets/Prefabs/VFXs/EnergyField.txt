Shader "Economy/EnergyProjector_Texture"
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
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            // Textures
            TEXTURE2D(_HeatmapTex);
            SAMPLER(sampler_HeatmapTex);

            float4 _MapCoords; // xy = Size, zw = Center Offset
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
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. Calculate Screen UVs
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // 2. Sample Depth & Skybox Check
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    if(rawDepth < 0.0001) discard;
                #else
                    if(rawDepth > 0.9999) discard;
                #endif

                // 3. Reconstruct World Position
                float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);

                // 4. Convert World Position to Heatmap Texture UVs
                // Formula: UV = ((WorldPos - Center) / Size) + 0.5
                float2 mapUV = (worldPos.xz - _MapCoords.zw) / _MapCoords.xy;
                mapUV += 0.5;

                // 5. Optimization: Discard if outside the map area
                // This prevents the texture from streaking endlessly
                if(mapUV.x < 0 || mapUV.x > 1 || mapUV.y < 0 || mapUV.y > 1) discard;

                // 6. Sample the Baked Texture (O(1) Lookup)
                float energy = SAMPLE_TEXTURE2D(_HeatmapTex, sampler_HeatmapTex, mapUV).r;

                if (energy <= 0.01) discard;

                // 7. Coloring Logic
                float t = saturate(energy / _MaxEnergy);
                
                half4 col;
                half4 baseColor = lerp(_ColorLow, _ColorMid, t * 2.0);
                half4 highColor = lerp(_ColorMid, _ColorHigh, (t - 0.5) * 2.0);
                col = (t < 0.5) ? baseColor : highColor;

                // Add nice rim/contouring
                col.rgb += (t * 0.1); 
                col.a *= _GlobalAlpha;

                return col;
            }
            ENDHLSL
        }
    }
}
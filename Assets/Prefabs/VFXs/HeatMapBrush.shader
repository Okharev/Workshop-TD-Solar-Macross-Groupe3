Shader "Hidden/Economy/HeatmapBrush"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Energy ("Energy", Float) = 1.0
        // How much of the circle is "solid" before fading? (0.0 = Cone, 0.8 = Hard Disc)
        _CoreRadius ("Core Radius", Range(0.0, 0.95)) = 0.5 
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend One One   // Additive
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            float _Energy;
            float _CoreRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distance from center (0 to 1 at edge)
                float dist = distance(i.uv, float2(0.5, 0.5)) * 2.0; 

                // --- FIX: SOLID CORE ---
                // smoothstep(edge0, edge1, x):
                // if x < edge0 (inside core) -> returns 0
                // if x > edge1 (outside) -> returns 1
                // We invert it (1.0 - val) so Center is White, Edge is Black.
                float circle = 1.0 - smoothstep(_CoreRadius, 1.0, dist);

                return float4(circle * _Energy, 0, 0, 1);
            }
            ENDCG
        }
    }
}
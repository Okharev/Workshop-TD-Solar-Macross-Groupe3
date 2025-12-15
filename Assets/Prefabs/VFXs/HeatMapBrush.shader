Shader "Hidden/Economy/SolidHeatmapBrush"
{
    Properties
    {
        _Energy ("Energy", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        // ZTest Always : On dessine toujours, même si un bâtiment est sous un autre dans la texture
        ZTest Always
        ZWrite Off
        Cull Off // On dessine les deux côtés des faces pour être sûr de tout remplir
        Blend One One // Additive (Les énergies s'additionnent si les zones se chevauchent)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float _Energy;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Retourne simplement la valeur de l'énergie dans le canal Rouge
                return float4(_Energy, 0, 0, 1);
            }
            ENDCG
        }
    }
}
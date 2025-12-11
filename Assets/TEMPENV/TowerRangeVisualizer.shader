Shader "Custom/TowerRangeVisualizer"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0, 0.5, 1, 0.2)
        _RimColor ("Fresnel Color (Edges)", Color) = (0, 1, 1, 1)
        _RimPower ("Fresnel Power", Range(0.5, 8.0)) = 3.0
        
        _MainTex ("Pattern Texture (Hex/Noise)", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed (X, Y)", Vector) = (0.1, 0.1, 0, 0)
    
        _IntersectionThreshold ("Intersection Depth Threshold", Float) = 1.0
        _IntersectionColor ("Intersection Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        // "Queue"="Transparent" assure le rendu après les objets opaques
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        // Pas d'écriture dans le Z-Buffer (pour voir à travers)
        ZWrite Off
        // Mélange Alpha standard (Alpha Blending)
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1; 
                float3 viewDir : TEXCOORD3;
                float3 normal : TEXCOORD4;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _RimColor;
            float _RimPower;
            float4 _ScrollSpeed;
            
            // Variable globale d'Unity pour la profondeur
            sampler2D _CameraDepthTexture;
            
            float _IntersectionThreshold;
            float4 _IntersectionColor;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                
                // Normales et Vue pour le Fresnel
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                
                // Animation de texture (Scroll)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Time.y * _ScrollSpeed.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Couleur de base + Texture
                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;
                
                // 2. Effet Fresnel (Bords brillants)
                float NdotV = 1.0 - saturate(dot(i.normal, i.viewDir));
                float rim = pow(NdotV, _RimPower);
                
                col.rgb += _RimColor.rgb * rim;
                col.a = max(col.a, rim * _RimColor.a); // On garde l'alpha le plus fort

                // 3. Intersection avec la profondeur (Depth Intersection)
                // Récupération de la profondeur de la scène derrière le pixel actuel
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                // Profondeur du pixel de notre sphère
                float partZ = i.screenPos.w; 
                
                float diff = sceneZ - partZ;

                // Si la différence est petite (on est proche d'un objet), on illumine
                if(diff > 0 && diff < _IntersectionThreshold)
                {
                    float intersectStrength = 1.0 - (diff / _IntersectionThreshold);
                    col.rgb += _IntersectionColor.rgb * intersectStrength;
                    col.a += intersectStrength; // Rend l'intersection plus opaque
                }

                return col;
            }
            ENDCG
        }
    }
}
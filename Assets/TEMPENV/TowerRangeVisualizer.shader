Shader "Custom/TowerRangeVisualizer "
{
    Properties
    {
        _MainColor ("Couleur Principale", Color) = (0, 0.5, 1, 0.5)
        _RimColor ("Couleur Fresnel (Bords)", Color) = (0, 1, 1, 1)
        _RimPower ("Puissance Fresnel", Range(0.5, 8.0)) = 3.0
        
        _MainTex ("Texture (Hex/Noise)", 2D) = "white" {}
        _ScrollSpeed ("Vitesse de défilement", Vector) = (0.1, 0.1, 0, 0)
        
        _IntersectionThreshold ("Seuil Intersection (Profondeur)", Float) = 1.0
        _IntersectionColor ("Couleur Intersection", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        ZWrite Off
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
            
            sampler2D _CameraDepthTexture;
            float _IntersectionThreshold;
            float4 _IntersectionColor;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                
                // Fresnel
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                
                // Texture scroll
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Time.y * _ScrollSpeed.xy;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Base texture
                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;

                // 2. Effet Fresnel Shining borders
                float NdotV = 1.0 - saturate(dot(i.normal, i.viewDir));
                float rim = pow(NdotV, _RimPower);
                col.rgb += _RimColor.rgb * rim;
                col.a = max(col.a, rim * _RimColor.a);

                // 3. Depth intersection
                float sceneZ = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)).r);
                float partZ = i.screenPos.w; // Profondeur de l'objet lui-même
                
                float diff = sceneZ - partZ;
                
                // if close to ground we highlight
                if(diff > 0 && diff < _IntersectionThreshold)
                {
                    float intersectStrength = 1.0 - (diff / _IntersectionThreshold);
                    col.rgb += _IntersectionColor.rgb * intersectStrength;
                    col.a += intersectStrength;
                }

                return col;
            }
            ENDCG
        }
    }
}
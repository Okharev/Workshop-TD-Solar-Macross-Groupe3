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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        // --- MODIFICATION 1 : VOIR L'INTÉRIEUR ---
        // "Cull Off" dit au GPU de ne pas cacher les faces arrière.
        // On voit maintenant l'intérieur et l'extérieur.
        Cull Off
        
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
                float3 normal : NORMAL; //
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
                o.vertex = UnityObjectToClipPos(v.vertex); //
                o.screenPos = ComputeScreenPos(o.vertex);
                
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex)); //
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Time.y * _ScrollSpeed.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Couleur de base
                fixed4 col = tex2D(_MainTex, i.uv) * _MainColor;

                // --- MODIFICATION 2 : FRESNEL DOUBLE FACE ---
                // Problème : À l'intérieur, la Normale et la Vue sont opposées.
                // Le "dot product" devient négatif, ce qui casse l'effet (tout devient blanc ou noir).
                // Solution : On utilise 'abs()' (valeur absolue) pour traiter l'intérieur comme l'extérieur.
                float NdotV = 1.0 - saturate(abs(dot(i.normal, i.viewDir))); //
                
                float rim = pow(NdotV, _RimPower); //
                
                col.rgb += _RimColor.rgb * rim;
                col.a = max(col.a, rim * _RimColor.a);

                // 3. Intersection Profondeur
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float partZ = i.screenPos.w; //
                float diff = sceneZ - partZ;

                if(diff > 0 && diff < _IntersectionThreshold)
                {
                    float intersectStrength = 1.0 - (diff / _IntersectionThreshold);
                    col.rgb += _IntersectionColor.rgb * intersectStrength; //
                    col.a += intersectStrength; 
                }

                return col;
            }
            ENDCG
        }
    }
}
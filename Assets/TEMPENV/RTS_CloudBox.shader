Shader "Custom/UltimateCloud_Final"
{
    Properties
    {
        [Header(Main Settings)]
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Self-Shadow Color", Color) = (0.6, 0.65, 0.75, 1)
        _ShadowStrength ("Self-Shadow Strength", Range(0, 1)) = 0.5
        _Seed ("Shape Variant", Float) = 1.0
        
        [Header(Flat Bottom)]
        // -0.1 is slightly below center. Increase to 0.0 or 0.1 to cut more off.
        _BottomFlatPos ("Bottom Level", Range(-0.5, 0.5)) = -0.15 
        _BottomSoftness ("Flatness Softness", Range(0.01, 0.5)) = 0.1
        
        [Header(Wind Interaction)]
        _WindResponsiveness ("Wind Sensitivity", Range(0, 2)) = 1.0
        
        [Header(Cluster Shape)]
        _CloudSize ("Base Size", Range(0.1, 0.5)) = 0.25
        _Spread ("Spread Width", Range(0, 1.0)) = 0.5
        _Padding ("Wall Padding", Range(0.0, 0.3)) = 0.1
        _Blend ("Blob Blending", Range(0.0001, 1.0)) = 0.4
        
        [Header(Detail)]
        _NoiseScale ("Noise Scale", Float) = 2.0
        _NoiseStr ("Noise Strength", Range(0, 0.2)) = 0.05
        _Turbulence ("Internal Churn", Range(0, 0.5)) = 0.15
        
        [Header(Lighting)]
        _LightDir ("Fake Sun Dir", Vector) = (0.5, 1, 0.2, 0)
        _ShadowBand ("Toon Shadow Sharpness", Range(0.01, 0.5)) = 0.05
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        CGINCLUDE
        #include "UnityCG.cginc"

        // --- PROPERTIES ---
        float4 _BaseColor;
        float4 _ShadowColor;
        float _ShadowStrength;
        float _Seed;
        
        float _BottomFlatPos;
        float _BottomSoftness;
        
        float _WindResponsiveness;
        float _CloudSize;
        float _Spread;
        float _Padding;
        float _Blend;
        float _NoiseScale;
        float _NoiseStr;
        float _Turbulence;
        float3 _LightDir;
        float _ShadowBand;

        // --- GLOBALS (From WindManager) ---
        float2 _WindDirection;      
        float _WindSpeed;           
        float _LocalWindStrength;   

        // --- MATH HELPERS ---
        float rand(float2 co){ return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453); }
        float hash(float n) { return frac(sin(n)*43758.5453); }
        
        // Noise Function
        float noise(float3 x) {
            float3 p = floor(x);
            float3 f = frac(x);
            f = f*f*(3.0-2.0*f);
            float n = p.x + p.y*57.0 + 113.0*p.z;
            return lerp(lerp(lerp(hash(n+0.0), hash(n+1.0),f.x), lerp(hash(n+57.0), hash(n+58.0),f.x),f.y),
                        lerp(lerp(hash(n+113.0), hash(n+114.0),f.x), lerp(hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);
        }
        
        // Smooth Min (Union - Blends shapes together)
        float smin(float a, float b, float k) { 
            float h = clamp(0.5 + 0.5*(b-a)/k, 0.0, 1.0); 
            return lerp(b, a, h) - k*h*(1.0-h); 
        }
        
        // Smooth Max (Intersection - Cuts shape B from A)
        // FIXED: This logic correctly prefers the Maximum value (Intersection)
        float smax(float a, float b, float k) {
            float h = clamp( 0.5 + 0.5*(a-b)/k, 0.0, 1.0 );
            return lerp( b, a, h ) + k*h*(1.0-h);
        }

        // --- MAIN SDF FUNCTION ---
        float map(float3 p) {
            // 1. WIND CALCULATIONS
            float totalWindForce = _LocalWindStrength * _WindResponsiveness;
            
            // Wind Skew (Leaning)
            float skewFactor = p.y * totalWindForce * 0.5;
            p.x -= _WindDirection.x * skewFactor;
            p.z -= _WindDirection.y * skewFactor;

            // Wind Scroll
            float scrollSpeed = _WindSpeed * (1.0 + totalWindForce);
            float3 windOffset = float3(_WindDirection.x, 0, _WindDirection.y) * _Time.y * scrollSpeed;
            
            // 2. NOISE LAYER
            float n = noise((p * _NoiseScale) - windOffset);
            
            // 3. CLUSTER GENERATION
            float cloudDist = 100.0;
            
            for(int i = 0; i < 5; i++) {
                float r1 = rand(float2(i, _Seed));     
                float r2 = rand(float2(i + 13.0, _Seed)); 
                float r3 = rand(float2(i + 29.0, _Seed)); 
                float rSize = rand(float2(i + 7.0, _Seed)); 

                // Internal Turbulence
                float t = _Time.y * (_Turbulence + (totalWindForce * 0.2)); 
                float3 motion = float3(sin(t + r1*10), cos(t*0.9 + r2*10), sin(t*1.1 + r3*10)) * 0.1;

                // Base Position
                float3 basePos = float3((r1 - 0.5) * _Spread * 2.0, (r2 - 0.5) * _Spread * 0.5, (r3 - 0.5) * _Spread);
                
                // Radius & Padding
                float radius = _CloudSize * (0.6 + rSize * 0.6); 
                float safeLimit = max(0.0, 0.5 - radius - _Padding);
                
                float3 finalPos = basePos + motion;
                finalPos = clamp(finalPos, -safeLimit, safeLimit);
                
                float d = length(p - finalPos) - radius;
                
                if (i == 0) cloudDist = d;
                else cloudDist = smin(cloudDist, d, _Blend);
            }
            
            // Apply Noise
            cloudDist += (n * _NoiseStr);

            // 4. FLAT BOTTOM LOGIC
            // We calculate the SDF of the "Sky" (Everything ABOVE the floor line).
            // Logic: distance = floorLevel - p.y
            // If p.y is HIGH (Sky), distance is NEGATIVE (Inside Sky).
            // If p.y is LOW (Ground), distance is POSITIVE (Outside Sky).
            float skyRegion = _BottomFlatPos - p.y;
            
            // Intersection: We only draw where we are Inside the Cloud AND Inside the Sky.
            // smax takes the Max distance (Intersection).
            return smax(cloudDist, skyRegion, _BottomSoftness);
        }

        // --- RAY-BOX INTERSECTION ---
        float2 boxIntersection(float3 ro, float3 rd, float3 boxSize) {
            float3 m = 1.0 / rd;
            float3 n = m * ro;
            float3 k = abs(m) * boxSize;
            float3 t1 = -n - k;
            float3 t2 = -n + k;
            float tN = max( max( t1.x, t1.y ), t1.z );
            float tF = min( min( t2.x, t2.y ), t2.z );
            if( tN > tF || tF < 0.0) return float2(-1.0, -1.0);
            return float2( tN, tF );
        }
        ENDCG

        // =========================================================
        // PASS 1: FORWARD RENDERING
        // =========================================================
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Front 

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 localPos : TEXCOORD0; };

            float3 calcNormal(float3 p) {
                const float h = 0.005; 
                const float2 k = float2(1,-1);
                return normalize(k.xyy*map(p + k.xyy*h) + k.yyx*map(p + k.yyx*h) + k.yxy*map(p + k.yxy*h) + k.xxx*map(p + k.xxx*h));
            }

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float3 camLocal = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 rd = normalize(i.localPos - camLocal);
                float3 ro = camLocal;

                float2 bounds = boxIntersection(ro, rd, float3(0.5, 0.5, 0.5));
                if (bounds.x == -1.0) discard;

                float t = max(0.0, bounds.x);
                float tMax = bounds.y;
                
                // Raymarch
                for(int j=0; j<64; j++) {
                    if(t >= tMax) break;
                    float3 p = ro + rd * t;
                    float d = map(p);
                    
                    if(d < 0.001) { 
                        float3 normal = calcNormal(p);
                        float3 lightDir = normalize(_LightDir);
                        
                        float NdotL = dot(normal, lightDir);
                        float lightIntensity = smoothstep(0.0, _ShadowBand, NdotL);
                        
                        float rim = 1.0 - saturate(dot(-rd, normal));
                        rim = pow(rim, 3.0);
                        
                        float3 finalColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, lightIntensity * (1.0 - _ShadowStrength) + _ShadowStrength);
                        if(lightIntensity < 0.5) finalColor *= (1.0 - _ShadowStrength * 0.5);

                        finalColor += rim * 0.5;
                        return float4(finalColor, 1.0);
                    }
                    t += d;
                }
                discard;
                return 0;
            }
            ENDCG
        }
        
        // =========================================================
        // PASS 2: SHADOW CASTER
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Off 

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma target 3.0

            struct appdata { 
                float4 vertex : POSITION; 
                float3 normal : NORMAL; 
            };
            
            struct v2f { 
                V2F_SHADOW_CASTER; 
                float3 localPos : TEXCOORD1; 
                float3 lightDir : TEXCOORD3; // Changed from viewDir to lightDir
            };

            float dither(float2 uv) { return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5; }

            v2f vert(appdata v) {
                v2f o;
                o.localPos = v.vertex.xyz;
                
                // IMPORTANT FIX: Use ObjSpaceLightDir
                // This returns the vector FROM the vertex TO the light source.
                // It correctly handles the World->Object rotation.
                o.lightDir = ObjSpaceLightDir(v.vertex);
                
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                float3 ro = i.localPos;
                
                // Ray Direction: 
                // i.lightDir points TO the light.
                // We want to march INTO the cloud (AWAY from the light).
                float3 rd = -normalize(i.lightDir); 

                // --- Standard Ray-Box Intersection ---
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3(0.5, 0.5, 0.5);
                float3 t1 = (boxMin - ro) / rd;
                float3 t2 = (boxMax - ro) / rd;
                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);
                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);

                float t = max(0.0, tNear);
                float maxDist = max(0.0, tFar);
                
                if (maxDist <= 0.0 || tNear > tFar) clip(-1);

                // --- Volumetric Raymarch ---
                t += dither(i.pos.xy * 0.5) * 0.05; 
                bool hit = false;
                
                for(int j=0; j<25; j++) {
                    if(t >= maxDist) break;
                    float3 p = ro + rd * t;
                    
                    // We check map(p). If d < 0.001, we hit cloud matter.
                    // This creates the "Cutout" for the shadow map.
                    float d = map(p); 
                    
                    if(d < 0.001) { hit = true; break; }
                    t += max(0.02, d);
                }

                if (!hit) clip(-1);
                return 0;
            }
            ENDCG
        }
    }
}
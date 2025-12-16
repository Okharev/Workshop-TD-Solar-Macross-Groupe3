Shader "Custom/GenshinFoliageUltimate"
{
    Properties
    {
        [Header(Base Settings)]
        [MainTexture] _BaseMap("Leaf Texture (Alpha for Cutout)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Geometry)]
        _NormalSpherize("Fluffy Normals (0=Flat, 1=Round)", Range(0, 1)) = 0.5

        [Header(Genshin Stylization)]
        _ColorTop("High Color (Sun)", Color) = (0.5, 0.8, 0.5, 1)
        _ColorBot("Low Color (Shadow)", Color) = (0.2, 0.4, 0.2, 1)
        _GradientScale("Gradient Scale", Float) = 10.0
        _GradientOffset("Gradient Offset", Float) = 0.0
        
        [Header(Lighting)]
        _RampTex("Toon Ramp (Black to White)", 2D) = "white" {}
        _ShadowTint("Shadow Tint (Color of Shadows)", Color) = (0.3, 0.3, 0.5, 1)
        
        [Header(Specular)]
        _SpecularColor("Specular Color", Color) = (0.9, 1.0, 0.8, 1)
        _Smoothness("Specular Smoothness", Range(1, 50)) = 15.0

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1, 1, 0.8, 1)
        _RimPower("Rim Power (Sharpness)", Range(0.1, 10)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 5)) = 0.5

        [Header(Translucency)]
        _TranslucencyColor("Translucency Color", Color) = (0.8, 1, 0.5, 1)
        _TranslucencyPower("Translucency Power", Range(0, 10)) = 5.0
        _TranslucencyDistortion("Translucency Distortion", Range(0, 1)) = 0.2

        [Header(Wind Settings)]
        _WindMultiplier("Overall Wind Strength", Range(0, 5)) = 1.0
        _SwayFrequency("Ambient Sway Speed", Float) = 1.0
        _LeafFlutter("Leaf Flutter Intensity", Range(0, 1)) = 0.2
        
        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (0.9, 1.0, 0.7, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.2
        
        [Header(Quality Of Life)]
        _EdgeFadePower("Edge Fade Power (Hide Paper Thin)", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="TransparentCutout" 
            "Queue"="AlphaTest" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
                half _NormalSpherize; 
                half4 _ColorTop;
                half4 _ColorBot;
                float _GradientScale;
                float _GradientOffset;
                half4 _ShadowTint;
                half4 _SpecularColor;
                half _Smoothness;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _TranslucencyColor;
                half _TranslucencyPower;
                half _TranslucencyDistortion;
                half _WindMultiplier;
                half _SwayFrequency;
                half _LeafFlutter;
                half4 _WaveColor;
                half _WaveOpacity;
                half _EdgeFadePower;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);
            
            // Global Wind Inputs (Set by Script)
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;
        ENDHLSL

        // --- PASS 1 : FORWARD LIT (Rendu Visible) ---
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off // Rendu double face

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog 
            #pragma multi_compile_instancing 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 color : COLOR; 
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD3;
                float windMask : TEXCOORD4;
                float4 color : COLOR;
                float randomHash : TEXCOORD5; 
                float fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                
                float3 positionWS = vertexInput.positionWS;
                float3 objectOrigin = GetObjectToWorldMatrix()._m03_m13_m23;

                // --- 1. COULEUR STABLE (Hash basé sur le Pivot) ---
                output.randomHash = frac(sin(dot(objectOrigin.xz, float2(12.9898, 78.233))) * 43758.5453);

                // --- 2. GESTION DES NORMALES "FLUFFY" ---
                // Normale sphérique : du centre de l'arbre vers la feuille
                float3 sphericalNormal = normalize(positionWS - objectOrigin);
                output.normalWS = lerp(normalInput.normalWS, sphericalNormal, _NormalSpherize);

                // --- 3. WIND PHYSICS COMPLÈTE ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;
                
                // Optimisation LOD : Réduit le tremblement si la caméra est loin
                float dist = distance(_WorldSpaceCameraPos, positionWS);
                float distMask = 1.0 - saturate((dist - 30.0) / 40.0);

                float windWeight = input.color.r;
                
                // Sway (Balancement Tronc)
                float swayTime = _Time.y * _SwayFrequency + (positionWS.x + positionWS.z) * 0.5;
                float ambientSway = sin(swayTime) * 0.1;

                // Flutter (Vibration Feuilles)
                float flutterFreq = _Time.y * 15.0 + dot(input.positionOS.xyz, float3(10,10,10));
                float flutter = sin(flutterFreq) * _LeafFlutter * gust * windWeight * distMask;

                // Total
                float totalPush = (gust * _GlobalWindStrength + ambientSway) * _WindMultiplier * windWeight;
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                displacement.y -= totalPush * totalPush * 0.2; 
                displacement.xyz += output.normalWS * flutter; // Utilise la normale sphérique pour le flutter

                positionWS += displacement;

                // --- OUTPUTS ---
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.windMask = gust * _GlobalWindStrength;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // --- 1. Edge Fading ---
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 geometricNormal = normalize(cross(ddyPos, ddxPos));
                float geometricNdotV = abs(dot(geometricNormal, viewDir));
                float edgeAlpha = pow(smoothstep(0.1, 1.0, geometricNdotV), _EdgeFadePower);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip((texColor.a * edgeAlpha) - _Cutoff);

                // --- 2. Couleur & Variation ---
                float randomHash = input.randomHash; 
                float brightnessVar = lerp(0.9, 1.1, randomHash); 
                half3 shiftColor = lerp(half3(1, 0.95, 0.95), half3(0.95, 1, 1), randomHash);

                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                half3 gradientColor = lerp(_ColorBot.rgb, _ColorTop.rgb, heightFactor);
                
                // [AMÉLIORATION 1] Vertex AO : On utilise le canal VERT (g) pour assombrir le coeur de l'arbre
                // Si tu ne peins pas tes vertex, le défaut est blanc (1), donc aucun changement.
                float vertexAO = input.color.g; 
                
                half3 albedo = texColor.rgb * gradientColor * input.color.rgb * brightnessVar * shiftColor;

                // --- 3. Gestion Double Face & Teinte ---
                half3 normal = normalize(input.normalWS);
                
                // [AMÉLIORATION 2] Backface Tint : Le dessous est légèrement plus sombre et bleuté
                if (!isFrontFace) 
                {
                    normal = -normal; 
                    albedo *= half3(0.9, 0.95, 1.0) * 0.8; // Teinte subtile "ombre froide"
                }

                // --- 4. Éclairage ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);

                float NdotL = dot(normal, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5;
                float shadowAtten = mainLight.shadowAttenuation;
                float lightIntensity = halfLambert * shadowAtten * vertexAO; // On applique l'AO ici

                half3 rampSample = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(lightIntensity, 0.5)).rgb;
                float rampBrightness = (rampSample.r + rampSample.g + rampSample.b) / 3.0;
                half3 lightColorResult = lerp(_ShadowTint.rgb, mainLight.color, rampBrightness);

                // --- 5. Effets Secondaires ---
                
                // Translucidité
                half3 transLightDir = lightDir + normal * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                half3 translucency = transDot * _TranslucencyColor.rgb * mainLight.color * shadowAtten * vertexAO;

                // Rim Light (Corrigé : respecte les ombres)
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float rimTerm = pow(fresnel, _RimPower);
                float rimLightAlign = saturate(dot(normal, lightDir)); 
                float rimLimit = rimLightAlign * shadowAtten * vertexAO; // AO masque aussi le rim interne
                half3 rimLight = _RimColor.rgb * rimTerm * _RimIntensity * rimLimit;

                // Specular Stylisé (CORRIGÉ & ADOUCI)
                half3 halfVector = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfVector));
                float specTerm = pow(NdotH, _Smoothness);
                specTerm = smoothstep(0.5, 0.9, specTerm); // Seuil strict pour éviter la tache blanche
                float specMask = shadowAtten * saturate(dot(normal, lightDir));
                
                // Intensité réduite (0.3) pour que ce soit subtil
                half3 specular = _SpecularColor.rgb * specTerm * specMask * mainLight.color * 0.3;

                // --- 6. Composition Finale ---
                float highlight = input.windMask * _WaveOpacity;
                half3 windVisualColor = _WaveColor.rgb * highlight;
                half3 ambient = SampleSH(normal) * vertexAO;

                half3 finalColor = albedo * (lightColorResult + ambient) + translucency + rimLight + specular + windVisualColor;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // --- PASS 2 : SHADOW CASTER (Stable Shadows) ---
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes 
            { 
                float4 positionOS : POSITION; 
                float2 texcoord : TEXCOORD0; 
                float4 color : COLOR; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings 
            { 
                float4 positionCS : SV_POSITION; 
                float2 uv : TEXCOORD0; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = vertexInput.positionWS;

                // --- VENT SIMPLIFIÉ (Pas de Flutter pour ombres stables) ---
                float time = _Time.y * _WindSpeed;
                float swayTime = time * _SwayFrequency + (positionWS.x + positionWS.z) * 0.5;
                float ambientSway = sin(swayTime) * 0.1;
                
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;

                float windWeight = input.color.r;
                float totalPush = (gust * _GlobalWindStrength + ambientSway) * _WindMultiplier * windWeight;
                
                positionWS.xz += _WindDirection * totalPush;

                output.positionCS = TransformWorldToHClip(positionWS);
                
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
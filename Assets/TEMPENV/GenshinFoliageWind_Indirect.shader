Shader "Custom/GenshinFoliageUltimate_Merged"
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
        _GradientScale("Gradient Scale", Float) = 8.0 // Valeur par défaut ajustée [cite: 2]
        _GradientOffset("Gradient Offset", Float) = 0.0

        [Header(Color Variation)]
        [Toggle] _EnableColorVar("Enable Position Variation", Float) = 1.0
        _ColorVariation("Variation Tint", Color) = (0.6, 0.8, 0.4, 1)
        _VariationScale("Variation Scale", Float) = 0.5
        _VariationPower("Variation Intensity", Range(0, 1)) = 0.2

        [Header(Lighting)]
        _RampTex("Toon Ramp (Black to White)", 2D) = "white" {}
        _ShadowTint("Shadow Tint (Color of Shadows)", Color) = (0.3, 0.3, 0.5, 1)
        // [FEATURE IMPORTANTE] Contrôle la force de l'ombre reçue pour éviter le bruit [cite: 4]
        _ReceiveShadowStrength("Received Shadow Strength", Range(0, 1)) = 0.5 
        
        [Header(Specular)]
        _SpecularColor("Specular Color", Color) = (0.9, 1.0, 0.8, 1)
        _Smoothness("Specular Smoothness", Range(1, 50)) = 15.0
        _SpecularIntensity("Specular Intensity", Range(0, 5)) = 0.5

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
                
                float _EnableColorVar;
                half4 _ColorVariation;
                float _VariationScale;
                float _VariationPower;

                half4 _ShadowTint;
                half _ReceiveShadowStrength; // 

                half4 _SpecularColor;
                half _Smoothness;
                half _SpecularIntensity;

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
            
            // Global Wind Inputs
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;
        ENDHLSL

        // --- PASS 1 : FORWARD LIT ---
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

                // --- 1. COULEUR STABLE ---
                output.randomHash = frac(sin(dot(objectOrigin.xz, float2(12.9898, 78.233))) * 43758.5453);

                // --- 2. FLUFFY NORMALS ---
                float3 sphericalNormal = normalize(positionWS - objectOrigin);
                output.normalWS = lerp(normalInput.normalWS, sphericalNormal, _NormalSpherize);

                // --- 3. WIND PHYSICS ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;
                
                // Distance Mask
                float dist = distance(_WorldSpaceCameraPos, positionWS);
                float distMask = 1.0 - saturate((dist - 30.0) / 40.0);

                float windWeight = input.color.r;
                
                // Sway + Flutter
                float swayTime = _Time.y * _SwayFrequency + (positionWS.x + positionWS.z) * 0.5;
                float ambientSway = sin(swayTime) * 0.1;
                float flutterFreq = _Time.y * 15.0 + dot(input.positionOS.xyz, float3(10,10,10));
                float flutter = sin(flutterFreq) * _LeafFlutter * gust * windWeight * distMask;

                float totalPush = (gust * _GlobalWindStrength + ambientSway) * _WindMultiplier * windWeight;
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                displacement.y -= totalPush * totalPush * 0.2; 
                displacement.xyz += output.normalWS * flutter;

                positionWS += displacement;

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
                
                // Edge Fade
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 geometricNormal = normalize(cross(ddyPos, ddxPos));
                float geometricNdotV = abs(dot(geometricNormal, viewDir));
                float edgeAlpha = pow(smoothstep(0.1, 1.0, geometricNdotV), _EdgeFadePower);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip((texColor.a * edgeAlpha) - _Cutoff);

                // --- GRADIENT LISSÉ & VARIATION [cite: 33, 34, 35, 36] ---
                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                half3 gradientColor = lerp(_ColorBot.rgb, _ColorTop.rgb, pow(heightFactor, 0.8)); // Curve 'pow' pour adoucir
                
                if(_EnableColorVar > 0.5)
                {
                    // Utilisation du hash stable calculé dans le vertex
                    float varNoise = input.randomHash; 
                    gradientColor = lerp(gradientColor, _ColorVariation.rgb, (varNoise * 0.5) * _VariationPower);
                }

                // AO (Canal Vert)
                float vertexAO = input.color.g; 
                half3 albedo = texColor.rgb * gradientColor * input.color.rgb;

                // --- NORMALE & BACKFACE ---
                half3 normal = normalize(input.normalWS);
                if (!isFrontFace) 
                {
                    normal = -normal; 
                    albedo *= half3(0.9, 0.95, 1.0) * 0.8; 
                }

                // --- ECLAIRAGE & SHADOW DE-NOISING  ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);

                float rawShadowAtten = mainLight.shadowAttenuation;
                
                // MAGIE ICI : On "nettoie" l'ombre. Si Strength = 0, l'ombre disparaît. Si 1, elle est noire.
                // Cela élimine le bruit des feuilles.
                float cleanShadowAtten = lerp(1.0, rawShadowAtten, _ReceiveShadowStrength);

                // Ramp Calculation (Découplé de l'ombre) [cite: 44, 45]
                float NdotL = dot(normal, lightDir);
                float lightingShape = NdotL * 0.5 + 0.5;
                half3 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(lightingShape, 0.5)).rgb;

                // Application de l'ombre colorée APRES le ramp [cite: 47]
                half3 lightColorResult = lerp(_ShadowTint.rgb, mainLight.color, cleanShadowAtten);
                
                // --- EFFETS SECONDAIRES (Masqués par cleanShadowAtten) ---
                
                // Translucidité
                half3 transLightDir = lightDir + normal * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                half3 translucency = transDot * _TranslucencyColor.rgb * mainLight.color * cleanShadowAtten * vertexAO;

                // Rim Light
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float rimTerm = pow(fresnel, _RimPower);
                float rimLimit = saturate(dot(normal, lightDir)); 
                half3 rimLight = _RimColor.rgb * rimTerm * _RimIntensity * rimLimit * cleanShadowAtten * vertexAO;

                // Specular Stylisé
                half3 halfVector = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfVector));
                float specTerm = pow(NdotH, _Smoothness);
                specTerm = smoothstep(0.5, 0.9, specTerm); 
                // Specular masqué par l'ombre "propre"
                half3 specular = _SpecularColor.rgb * specTerm * _SpecularIntensity * cleanShadowAtten * saturate(dot(normal, lightDir)) * mainLight.color;

                // --- COMPOSITION ---
                float highlight = input.windMask * _WaveOpacity;
                half3 windVisualColor = _WaveColor.rgb * highlight * mainLight.color;
                half3 ambient = SampleSH(normal) * vertexAO;

                half3 finalColor = albedo * (lightColorResult * rampColor + ambient) + translucency + rimLight + specular + windVisualColor;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // --- PASS 2 : SHADOW CASTER (Stable Sway Only) ---
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

                // WIND STABLE (Sway uniquement, pas de flutter pour éviter le scintillement)
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
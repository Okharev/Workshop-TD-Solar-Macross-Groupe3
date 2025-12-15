Shader "Custom/GenshinFoliageWind_Clean"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap("Leaf Texture (Atlas 2x2)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Genshin Stylization)]
        _ColorTop("High Color (Sun)", Color) = (0.5, 0.8, 0.5, 1)
        _ColorBot("Low Color (Shadow)", Color) = (0.2, 0.4, 0.2, 1)
        // MODIFICATION 1 : Echelle réduite par défaut pour mieux voir le gradient
        _GradientScale("Gradient Scale", Float) = 8.0 
        _GradientOffset("Gradient Offset", Float) = 0.0

        [Header(Color Variation)]
        [Toggle(_)] _EnableColorVar("Enable Position Variation", Float) = 1.0
        _ColorVariation("Variation Tint", Color) = (0.6, 0.8, 0.4, 1)
        _VariationScale("Variation Scale", Float) = 0.1
        _VariationPower("Variation Intensity", Range(0, 1)) = 0.3
        
        [Header(Stylized Specular)]
        _SpecularColor("Specular Tint", Color) = (1, 1, 1, 1)
        _SpecularPower("Specular Size", Range(0.1, 100)) = 20.0
        _SpecularIntensity("Specular Intensity", Range(0, 5)) = 0.5

        [Header(Lighting and Shadows)]
        _RampTex("Toon Ramp (Black to White)", 2D) = "white" {}
        _ShadowTint("Shadow Tint", Color) = (0.3, 0.3, 0.5, 1)
        // MODIFICATION 2 : Nouveau paramètre pour contrôler le "bruit" des ombres
        _ReceiveShadowStrength("Received Shadow Strength", Range(0, 1)) = 0.5
        
        [Header(Debug)]
        [Toggle] _DebugShadows("Visualize Shadow Optimization", Float) = 0
        
        [Header(Rim Light)]
        _RimTint("Rim Tint", Color) = (1, 1, 1, 1)
        _RimPower("Rim Sharpness", Range(0.1, 20)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 5)) = 0.5

        [Header(Translucency)]
        _TranslucencyTint("Translucency Tint", Color) = (0.5, 0.8, 0.2, 1)
        _TranslucencyPower("Translucency Focus", Range(0, 20)) = 5.0
        _TranslucencyDistortion("Translucency Distortion", Range(0, 1)) = 0.2

        [Header(Wind Physics)]
        _WindMultiplier("Overall Wind Strength", Range(0, 5)) = 1.0
        _LeafFlutter("Leaf Flutter Intensity", Range(0, 1)) = 0.2
        
        [Header(Wind Visuals)]
        _WaveTint ("Gust Tint", Color) = (1, 1, 1, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.2
        
        [Header(Quality Of Life)]
        _EdgeFadePower("Edge Fade Power", Range(0, 20)) = 2.0
        _EdgeFadeOffset("Edge Fade Offset", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinWindPhysics.hlsl"

            // Globals
            float _GlobalWindStrength;
            float _WindSpeed; float _WindScale; float2 _WindDirection;
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
                half4 _ColorTop; half4 _ColorBot; float _GradientScale; float _GradientOffset;
                float _EnableColorVar; half4 _ColorVariation; float _VariationScale; float _VariationPower;
                half4 _SpecularColor; float _SpecularPower; float _SpecularIntensity;
                half4 _ShadowTint; half4 _RimTint; half _RimPower; half _RimIntensity;
                half4 _TranslucencyTint; half _TranslucencyPower; half _TranslucencyDistortion;
                half _WindMultiplier; half _LeafFlutter;
                half4 _WaveTint; half _WaveOpacity; half _EdgeFadePower; half _EdgeFadeOffset;
                float _DebugShadows;
                // Ajout CBUFFER
                half _ReceiveShadowStrength;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            struct Attributes { 
                float4 positionOS : POSITION; 
                float2 uv : TEXCOORD0; 
                float3 normalOS : NORMAL;
                float4 color : COLOR; 
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Varyings { 
                float4 positionCS : SV_POSITION; 
                float2 uv : TEXCOORD0; 
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD3; 
                float windMask : TEXCOORD4; 
                float4 color : COLOR; 
                float fogFactor : TEXCOORD5; 
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                output.normalWS = normalInput.normalWS;

                float time = _Time.y * _WindSpeed;
                float2 windUV = output.positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;

                float3 bending = CalculateMainBending(output.positionWS, input.color.r, input.color.a, _WindMultiplier, _WindDirection, _WindSpeed, _GlobalWindStrength, _WindMap, sampler_WindMap, _WindScale, time);
                float3 flutter = CalculateLeafFlutter(input.positionOS.xyz, output.normalWS, _LeafFlutter, time, input.color.g);

                output.positionWS += bending + flutter;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.windMask = gust * _GlobalWindStrength;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // --- 1. EDGE FADE ---
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 geometricNormal = normalize(cross(ddyPos, ddxPos));
                float geometricNdotV = abs(dot(geometricNormal, viewDir));
                float edgeAlpha = pow(smoothstep(_EdgeFadeOffset, 1.0, geometricNdotV), _EdgeFadePower);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip((texColor.a * edgeAlpha) - _Cutoff);

                // --- 2. GRADIENT FIX ---
                // Le gradient est calculé en World Space Y. 
                // Si l'arbre est à Y=0 et fait 10m de haut, heightFactor va de 0 à 1 (si Scale=10).
                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                // On applique une courbe "pow" pour rendre le gradient plus doux vers le haut
                half3 gradient = lerp(_ColorBot.rgb, _ColorTop.rgb, pow(heightFactor, 0.8));
                
                if(_EnableColorVar > 0.5) {
                    float variationNoise = sin(input.positionWS.x * _VariationScale) + cos(input.positionWS.z * _VariationScale * 0.8);
                    gradient = lerp(gradient, _ColorVariation.rgb, (variationNoise * 0.5 + 0.5) * _VariationPower);
                }
                
                // On applique l'AO (Blue channel) directement sur la couleur de base pour assombrir l'intérieur
                // Cela remplace les ombres "noisy" par des ombres "baking" plus propres.
                half3 albedo = texColor.rgb * gradient * input.color.b;

                // --- 3. LIGHTING & SHADOW NOISE FIX ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);
                half3 normal = normalize(input.normalWS); 
                if (!isFrontFace) normal = -normal;

                float NdotL = dot(normal, lightDir);
                
                // C'EST ICI QUE LA MAGIE OPERE :
                // On prend l'ombre reçue brute (très bruitée)
                float rawShadowAtten = mainLight.shadowAttenuation;
                
                // On la mélange avec 1 (pas d'ombre) selon le slider _ReceiveShadowStrength.
                // Si Strength = 0.2, les feuilles s'assombrissent à peine quand elles sont à l'ombre.
                // Cela élimine le contraste noir/blanc qui crée le bruit.
                float cleanShadowAtten = lerp(1.0, rawShadowAtten, _ReceiveShadowStrength);

                // Calcul du Ramp (Toon Shading)
                // On calcule d'abord l'éclairage de forme (NdotL)
                float lightingShape = NdotL * 0.5 + 0.5;
                // On lit la texture de Ramp
                half3 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(lightingShape, 0.5)).rgb;
                
                // On applique l'ombre reçue APRES le ramp, comme un "tint" léger, pas comme un masque noir absolu.
                half3 lightColorResult = lerp(_ShadowTint.rgb, mainLight.color, cleanShadowAtten);
                
                // --- 4. SPECULAR & TRANSLUCENCY ---
                half3 halfVector = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVector);
                float specular = smoothstep(0.5, 0.55, pow(saturate(NdotH), _SpecularPower));
                // Specular doit être masqué par l'ombre, mais on utilise cleanShadowAtten
                half3 specularReflection = mainLight.color * _SpecularColor.rgb * specular * _SpecularIntensity * cleanShadowAtten;

                half3 transLightDir = lightDir + normal * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                // Translucency aide à réduire l'effet sombre à l'intérieur
                half3 translucency = transDot * _TranslucencyTint.rgb * mainLight.color * cleanShadowAtten;

                // --- 5. RIM LIGHT ---
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float rimTerm = pow(fresnel, _RimPower);
                float rimLimit = saturate(dot(normal, lightDir) + 0.5);
                float internalOcclusion = pow(input.color.b, 4.0);
                half3 rimLight = mainLight.color * _RimTint.rgb * rimTerm * _RimIntensity * rimLimit * internalOcclusion * cleanShadowAtten;

                // --- COMPOSITION ---
                float gustHighlight = input.windMask * _WaveOpacity;
                half3 windVisualColor = mainLight.color * _WaveTint.rgb * gustHighlight;

                half3 ambient = SampleSH(normal);
                
                // Formule finale combinée
                half3 finalColor = albedo * (lightColorResult * rampColor + ambient) + translucency + rimLight + windVisualColor + specularReflection;

                if (_DebugShadows > 0.5)
                {
                    bool isCore = input.color.b < 0.65;
                    return isCore ? half4(1, 0, 0, 1) : half4(0, 1, 0, 1);
                }

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // --- PASS SHADOWCASTER RESTE IDENTIQUE (Optimisé) ---
        Pass {
            Name "ShadowCaster" Tags{"LightMode" = "ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GenshinWindPhysics.hlsl"

            float _GlobalWindStrength;
            float _WindSpeed; float _WindScale; float2 _WindDirection; TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half _Cutoff; half _WindMultiplier; half _LeafFlutter; half _EdgeFadePower; half _EdgeFadeOffset;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; float2 texcoord : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings ShadowPassVertex(Attributes input) { 
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz); 
                float3 positionWS = vertexInput.positionWS;
                float time = _Time.y * _WindSpeed;
                float3 bending = CalculateMainBending(positionWS, input.color.r, input.color.a, _WindMultiplier, _WindDirection, _WindSpeed, _GlobalWindStrength, _WindMap, sampler_WindMap, _WindScale, time);
                positionWS += bending; 
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap); output.positionWS = positionWS; output.color = input.color;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET { 
                UNITY_SETUP_INSTANCE_ID(input);
                float2 localUV = frac(input.uv * 2.0); 
                if (input.color.b < 0.65) { float dist = distance(localUV, float2(0.5, 0.5)); clip(0.5 - dist); }
                else { half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a; clip(alpha - _Cutoff); }
                return 0;
            }
            ENDHLSL
        }
    }
}
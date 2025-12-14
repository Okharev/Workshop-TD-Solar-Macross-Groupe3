Shader "Custom/GenshinFoliageWindFinal"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap("Leaf Texture (Alpha for Cutout)", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Genshin Stylization)]
        _ColorTop("High Color (Sun)", Color) = (0.5, 0.8, 0.5, 1)
        _ColorBot("Low Color (Shadow)", Color) = (0.2, 0.4, 0.2, 1)
        _GradientScale("Gradient Scale", Float) = 10.0
        _GradientOffset("Gradient Offset", Float) = 0.0
        
        [Header(Lighting)]
        _RampTex("Toon Ramp (Black to White)", 2D) = "white" {}
        _ShadowTint("Shadow Tint (Color of Shadows)", Color) = (0.3, 0.3, 0.5, 1)
        
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
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off // Rendu double face obligatoire pour les feuilles

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // --- GLOBAL WIND VARIABLES (GlobalWindManager.cs) ---
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
                
                half4 _ColorTop;
                half4 _ColorBot;
                float _GradientScale;
                float _GradientOffset;
                
                half4 _ShadowTint;
                
                // Rim Light
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                
                // Translucency
                half4 _TranslucencyColor;
                half _TranslucencyPower;
                half _TranslucencyDistortion;

                // Wind
                half _WindMultiplier;
                half _SwayFrequency;
                half _LeafFlutter;
                
                // Visuals
                half4 _WaveColor;
                half _WaveOpacity;
                half _EdgeFadePower;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // Rouge = Force du vent
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD3;
                float windMask : TEXCOORD4;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = vertexInput.positionWS;
                
                // Calcul immédiat des normales (requis pour le flutter)
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                float3 normalWS = normalInput.normalWS;

                // --- 1. WIND PHYSICS ---
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                
                // Échantillonnage texture de vent (Bruit)
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;

                float windWeight = input.color.r; // Vertex Color Red
                
                // A. Sway (Balancement lent)
                float swayTime = _Time.y * _SwayFrequency + (positionWS.x + positionWS.z) * 0.5;
                float ambientSway = sin(swayTime) * 0.1;

                // B. Flutter (Vibration rapide des feuilles)
                float flutterFreq = _Time.y * 15.0 + dot(input.positionOS.xyz, float3(10,10,10));
                float flutter = sin(flutterFreq) * _LeafFlutter * gust * windWeight;

                // C. Déplacement Total
                float totalPush = (gust * _GlobalWindStrength + ambientSway) * _WindMultiplier * windWeight;
                
                float3 displacement = float3(_WindDirection.x * totalPush, 0, _WindDirection.y * totalPush);
                displacement.y -= totalPush * totalPush * 0.2; // Arc vers le bas
                displacement.xyz += normalWS * flutter; // Vibration locale

                positionWS += displacement;

                // --- OUTPUTS ---
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.windMask = gust * _GlobalWindStrength;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // --- 1. Edge Fading (Anti-Papier) ---
                // Calcule la vraie normale géométrique pour savoir si on regarde la tranche
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 geometricNormal = normalize(cross(ddyPos, ddxPos));
                float geometricNdotV = abs(dot(geometricNormal, viewDir));
                
                // Alpha fade sur les bords
                float edgeAlpha = smoothstep(0.1, 1.0, geometricNdotV);
                edgeAlpha = pow(edgeAlpha, _EdgeFadePower);

                // Texture et Cutout
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip((texColor.a * edgeAlpha) - _Cutoff);

                // --- 2. Variation de Couleur (Random per Instance) ---
                // Hash basé sur la position XZ du monde (fixe par arbre)
                float randomHash = frac(sin(dot(input.positionWS.xz, float2(12.9898, 78.233))) * 43758.5453);
                float brightnessVar = lerp(0.9, 1.1, randomHash); // +/- 10% luminosité
                half3 shiftColor = lerp(half3(1, 0.95, 0.95), half3(0.95, 1, 1), randomHash); // Nuance chaude/froide

                // --- 3. Gradient Vertical & Base Albedo ---
                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                half3 gradientColor = lerp(_ColorBot.rgb, _ColorTop.rgb, heightFactor);
                
                // Combinaison : Texture * Gradient * AO(VertexColor) * Variation
                half3 albedo = texColor.rgb * gradientColor * input.color.rgb * brightnessVar * shiftColor;

                // --- 4. Éclairage (Toon & Shadow Tint) ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);
                half3 normal = normalize(input.normalWS); // Normale Sphérique "Fluffy"

                float NdotL = dot(normal, lightDir);
                float halfLambert = NdotL * 0.5 + 0.5;
                float shadowAtten = mainLight.shadowAttenuation;
                float lightIntensity = halfLambert * shadowAtten;

                // Toon Ramp Sample
                half3 rampSample = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(lightIntensity, 0.5)).rgb;
                float rampBrightness = (rampSample.r + rampSample.g + rampSample.b) / 3.0;

                // Mélange Ombre Teintée vs Lumière Soleil
                half3 lightColorResult = lerp(_ShadowTint.rgb, mainLight.color, rampBrightness);

                // --- 5. Translucidité ---
                half3 transLightDir = lightDir + normal * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                half3 translucency = transDot * _TranslucencyColor.rgb * mainLight.color * shadowAtten;

                // --- 6. Rim Light (NOUVEAU) ---
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float rimTerm = pow(fresnel, _RimPower);
                float rimLimit = saturate(dot(normal, lightDir) + 0.5); // Masquer le côté ombre (optionnel)
                half3 rimLight = _RimColor.rgb * rimTerm * _RimIntensity * rimLimit;

                // --- 7. Highlight du Vent ---
                float highlight = input.windMask * _WaveOpacity;
                half3 windVisualColor = _WaveColor.rgb * highlight;

                // --- 8. Composition Finale ---
                half3 ambient = SampleSH(normal);
                half3 finalColor = albedo * (lightColorResult + ambient) + translucency + rimLight + windVisualColor;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // Pass Ombres Portées (Simplifié)
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 texcoord : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed; float _WindScale; float2 _WindDirection; float _GlobalWindStrength;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half _Cutoff; half _WindMultiplier; half _SwayFrequency;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                float3 positionWS = vertexInput.positionWS;
                
                // Même logique vent simplifiée pour que l'ombre suive l'arbre
                float time = _Time.y * _WindSpeed;
                float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float windWeight = input.color.r;
                float totalPush = (noise * noise * _GlobalWindStrength + sin(time)) * _WindMultiplier * windWeight;
                positionWS.xz += _WindDirection * totalPush;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(texColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
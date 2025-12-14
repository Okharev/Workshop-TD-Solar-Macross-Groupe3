Shader "Custom/GenshinFoliageWind_Responsive"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseMap("Leaf Texture", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Genshin Stylization)]
        _ColorTop("High Color (Sun)", Color) = (0.5, 0.8, 0.5, 1)
        _ColorBot("Low Color (Shadow)", Color) = (0.2, 0.4, 0.2, 1)
        _GradientScale("Gradient Scale", Float) = 10.0
        _GradientOffset("Gradient Offset", Float) = 0.0

        [Header(Color Variation)]
        [Toggle(_)] _EnableColorVar("Enable Position Variation", Float) = 1.0
        _ColorVariation("Variation Tint", Color) = (0.6, 0.8, 0.4, 1)
        _VariationScale("Variation Scale (World Size)", Float) = 0.1
        _VariationPower("Variation Intensity", Range(0, 1)) = 0.3
        
        [Header(Stylized Specular)]
        _SpecularColor("Specular Tint", Color) = (1, 1, 1, 1) // Devenu un Tint
        _SpecularPower("Specular Size", Range(0.1, 100)) = 20.0
        _SpecularIntensity("Specular Intensity", Range(0, 5)) = 0.5

        [Header(Lighting)]
        _RampTex("Toon Ramp (Black to White)", 2D) = "white" {}
        _ShadowTint("Shadow Tint", Color) = (0.3, 0.3, 0.5, 1)

        [Header(Rim Light)]
        _RimTint("Rim Tint (Multiplies Light)", Color) = (1, 1, 1, 1) // Changé
        _RimPower("Rim Sharpness", Range(0.1, 20)) = 3.0
        _RimIntensity("Rim Intensity", Range(0, 5)) = 0.5

        [Header(Translucency)]
        _TranslucencyTint("Translucency Tint", Color) = (0.5, 0.8, 0.2, 1) // Changé
        _TranslucencyPower("Translucency Focus", Range(0, 20)) = 5.0
        _TranslucencyDistortion("Translucency Distortion", Range(0, 1)) = 0.2

        [Header(Wind Physics)]
        _WindMultiplier("Overall Wind Strength", Range(0, 5)) = 1.0
        _LeafFlutter("Leaf Flutter Intensity", Range(0, 1)) = 0.2
        
        [Header(Wind Visuals)]
        _WaveTint ("Gust Tint", Color) = (1, 1, 1, 1) // Changé
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.2
        
        [Header(Quality Of Life)]
        _EdgeFadePower("Edge Fade Power", Range(0, 20)) = 2.0
        _EdgeFadeOffset("Edge Fade Offset", Range(0, 1)) = 0.1
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
            Cull Off 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GenshinWindPhysics.hlsl"

            // --- INDIRECT SETUP (Standard) ---
            struct TreeData { float3 position; float4 rotation; float scale; float4 colorTint; };
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<TreeData> _TreeDataBuffer;
                StructuredBuffer<uint> _VisibleInstanceIndices;
            #endif
            float4x4 QuaternionToMatrix(float4 q) {
                float4x4 m = float4x4(1-2*q.y*q.y-2*q.z*q.z, 2*q.x*q.y-2*q.z*q.w, 2*q.x*q.z+2*q.y*q.w, 0, 2*q.x*q.y+2*q.z*q.w, 1-2*q.x*q.x-2*q.z*q.z, 2*q.y*q.z-2*q.x*q.w, 0, 2*q.x*q.z-2*q.y*q.w, 2*q.y*q.z+2*q.x*q.w, 1-2*q.x*q.x-2*q.y*q.y, 0, 0,0,0,1);
                return m;
            }
            void setup() {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                uint id = _VisibleInstanceIndices[unity_InstanceID];
                TreeData data = _TreeDataBuffer[id];
                float4x4 rot = QuaternionToMatrix(data.rotation);
                float4x4 scaleMat = float4x4(data.scale,0,0,0, 0,data.scale,0,0, 0,0,data.scale,0, 0,0,0,1);
                float4x4 posMat = float4x4(1,0,0,data.position.x, 0,1,0,data.position.y, 0,0,1,data.position.z, 0,0,0,1);
                unity_ObjectToWorld = mul(posMat, mul(rot, scaleMat));
                float invScale = 1.0 / data.scale;
                float4x4 invScaleMat = float4x4(invScale,0,0,0, 0,invScale,0,0, 0,0,invScale,0, 0,0,0,1);
                float4x4 invRotMat = transpose(rot);
                float4x4 invPosMat = float4x4(1,0,0,-data.position.x, 0,1,0,-data.position.y, 0,0,1,-data.position.z, 0,0,0,1);
                unity_WorldToObject = mul(invScaleMat, mul(invRotMat, invPosMat));
            #endif
            }

            // Globals
            float _GlobalWindStrength; float _WindSpeed; float _WindScale; float2 _WindDirection;
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half _Cutoff;
                half4 _ColorTop; half4 _ColorBot; float _GradientScale; float _GradientOffset;
                float _EnableColorVar; half4 _ColorVariation; float _VariationScale; float _VariationPower;
                
                half4 _SpecularColor; float _SpecularPower; float _SpecularIntensity;
                
                half4 _ShadowTint; 
                half4 _RimTint; // Renommé
                half _RimPower; half _RimIntensity;
                
                half4 _TranslucencyTint; // Renommé
                half _TranslucencyPower; half _TranslucencyDistortion;
                
                half _WindMultiplier; half _LeafFlutter;
                
                half4 _WaveTint; // Renommé
                half _WaveOpacity; half _EdgeFadePower; half _EdgeFadeOffset;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD3; float windMask : TEXCOORD4; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input);
                
                float4 instanceTint = float4(1,1,1,1);
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    uint id = _VisibleInstanceIndices[unity_InstanceID];
                    instanceTint = _TreeDataBuffer[id].colorTint;
                #endif

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                output.normalWS = normalInput.normalWS;

                // Physics
                float time = _Time.y * _WindSpeed;
                float2 windUV = output.positionWS.xz * _WindScale - (_WindDirection * time);
                float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
                float gust = noise * noise;
                float3 bending = CalculateMainBending(output.positionWS, input.color.r, input.color.a, _WindMultiplier, _WindDirection, _WindSpeed, _GlobalWindStrength, _WindMap, sampler_WindMap, _WindScale, time);
                float3 flutter = CalculateLeafFlutter(input.positionOS.xyz, output.normalWS, _LeafFlutter, time, input.color.g);

                output.positionWS += bending + flutter;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * instanceTint;
                output.windMask = gust * _GlobalWindStrength;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Edge Fade
                float3 ddxPos = ddx(input.positionWS);
                float3 ddyPos = ddy(input.positionWS);
                float3 geometricNormal = normalize(cross(ddyPos, ddxPos));
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float geometricNdotV = abs(dot(geometricNormal, viewDir));
                float edgeAlpha = pow(smoothstep(_EdgeFadeOffset, 1.0, geometricNdotV), _EdgeFadePower);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip((texColor.a * edgeAlpha) - _Cutoff);

                // Colors & Gradient
                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                half3 gradient = lerp(_ColorBot.rgb, _ColorTop.rgb, heightFactor);
                if(_EnableColorVar > 0.5) {
                    float variationNoise = sin(input.positionWS.x * _VariationScale) + cos(input.positionWS.z * _VariationScale * 0.8);
                    gradient = lerp(gradient, _ColorVariation.rgb, (variationNoise * 0.5 + 0.5) * _VariationPower);
                }
                half3 albedo = texColor.rgb * gradient * input.color.b * input.color.rgb;

                // --- LIGHTING ---
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightDir = normalize(mainLight.direction);
                half3 normal = normalize(input.normalWS); 
                float NdotL = dot(normal, lightDir);
                float shadowAtten = mainLight.shadowAttenuation;
                float rampUV = saturate((NdotL * 0.5 + 0.5) * shadowAtten);
                half3 rampColor = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampUV, 0.5)).rgb;
                
                // La couleur du résultat lumière prend en compte la lumière principale
                half3 lightColorResult = lerp(_ShadowTint.rgb, mainLight.color, (rampColor.r + rampColor.g + rampColor.b) / 3.0);

                // --- 1. SPECULAR (INFERRED) ---
                half3 halfVector = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVector);
                float specular = smoothstep(0.5, 0.55, pow(saturate(NdotH), _SpecularPower));
                // On utilise mainLight.color ici !
                half3 specularReflection = mainLight.color * _SpecularColor.rgb * specular * _SpecularIntensity * shadowAtten;

                // --- 2. TRANSLUCENCY (INFERRED) ---
                half3 transLightDir = lightDir + normal * _TranslucencyDistortion;
                float transDot = pow(saturate(dot(viewDir, -transLightDir)), _TranslucencyPower);
                // On utilise mainLight.color ! La lumière qui passe au travers est celle du soleil
                half3 translucency = transDot * _TranslucencyTint.rgb * mainLight.color * shadowAtten;

                // --- 3. RIM LIGHT (INFERRED) ---
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float rimTerm = pow(fresnel, _RimPower);
                float rimLimit = saturate(dot(normal, lightDir) + 0.5);
                // Le Rim est illuminé par le soleil, donc mainLight.color
                half3 rimLight = mainLight.color * _RimTint.rgb * rimTerm * _RimIntensity * rimLimit;

                // --- 4. WIND VISUALS (INFERRED) ---
                float gustHighlight = input.windMask * _WaveOpacity;
                // Les rafales brillent avec la couleur du soleil
                half3 windVisualColor = mainLight.color * _WaveTint.rgb * gustHighlight;

                // Final
                half3 ambient = SampleSH(normal);
                half3 finalColor = albedo * (lightColorResult * rampColor + ambient) 
                                 + translucency 
                                 + rimLight 
                                 + windVisualColor
                                 + specularReflection;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // Shadow Pass (Standard, sans changement nécessaire sauf CBUFFER)
        Pass {
            Name "ShadowCaster" Tags{"LightMode" = "ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GenshinWindPhysics.hlsl"

            struct TreeData { float3 position; float4 rotation; float scale; float4 colorTint; };
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<TreeData> _TreeDataBuffer; StructuredBuffer<uint> _VisibleInstanceIndices;
            #endif
            float4x4 QuaternionToMatrix(float4 q) { float4x4 m = float4x4(1-2*q.y*q.y-2*q.z*q.z, 2*q.x*q.y-2*q.z*q.w, 2*q.x*q.z+2*q.y*q.w, 0, 2*q.x*q.y+2*q.z*q.w, 1-2*q.x*q.x-2*q.z*q.z, 2*q.y*q.z-2*q.x*q.w, 0, 2*q.x*q.z-2*q.y*q.w, 2*q.y*q.z+2*q.x*q.w, 1-2*q.x*q.x-2*q.y*q.y, 0, 0,0,0,1); return m; }
            void setup() {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                uint id = _VisibleInstanceIndices[unity_InstanceID]; TreeData data = _TreeDataBuffer[id];
                float4x4 rot = QuaternionToMatrix(data.rotation); float4x4 scaleMat = float4x4(data.scale,0,0,0, 0,data.scale,0,0, 0,0,data.scale,0, 0,0,0,1);
                float4x4 posMat = float4x4(1,0,0,data.position.x, 0,1,0,data.position.y, 0,0,1,data.position.z, 0,0,0,1);
                unity_ObjectToWorld = mul(posMat, mul(rot, scaleMat));
                float invScale = 1.0 / data.scale; float4x4 invScaleMat = float4x4(invScale,0,0,0, 0,invScale,0,0, 0,0,invScale,0, 0,0,0,1); float4x4 invRotMat = transpose(rot); float4x4 invPosMat = float4x4(1,0,0,-data.position.x, 0,1,0,-data.position.y, 0,0,1,-data.position.z, 0,0,0,1);
                unity_WorldToObject = mul(invScaleMat, mul(invRotMat, invPosMat));
            #endif
            }
            float _GlobalWindStrength; float _WindSpeed; float _WindScale; float2 _WindDirection; TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half _Cutoff;
                half4 _ColorTop; half4 _ColorBot; float _GradientScale; float _GradientOffset;
                float _EnableColorVar; half4 _ColorVariation; float _VariationScale; float _VariationPower;
                half4 _SpecularColor; float _SpecularPower; float _SpecularIntensity;
                half4 _ShadowTint; half4 _RimTint; half _RimPower; half _RimIntensity;
                half4 _TranslucencyTint; half _TranslucencyPower; half _TranslucencyDistortion;
                half _WindMultiplier; half _LeafFlutter;
                half4 _WaveTint; half _WaveOpacity; half _EdgeFadePower; half _EdgeFadeOffset;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; float2 texcoord : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings ShadowPassVertex(Attributes input) { Varyings output; UNITY_SETUP_INSTANCE_ID(input); VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz); float3 positionWS = vertexInput.positionWS; float time = _Time.y * _WindSpeed; float3 bending = CalculateMainBending(positionWS, input.color.r, input.color.a, _WindMultiplier, _WindDirection, _WindSpeed, _GlobalWindStrength, _WindMap, sampler_WindMap, _WindScale, time); positionWS += bending; output.positionCS = TransformWorldToHClip(positionWS); output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap); output.positionWS = positionWS; return output; }
            half4 ShadowPassFragment(Varyings input) : SV_TARGET { half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv); float3 ddxPos = ddx(input.positionWS); float3 ddyPos = ddy(input.positionWS); float3 geometricNormal = normalize(cross(ddyPos, ddxPos)); half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS); float geometricNdotV = abs(dot(geometricNormal, viewDir)); float edgeAlpha = pow(smoothstep(_EdgeFadeOffset, 1.0, geometricNdotV), _EdgeFadePower); clip((texColor.a * edgeAlpha) - _Cutoff); return 0; }
            ENDHLSL
        }
    }
}
Shader "Custom/GenshinTrunkWind_Clean"
{
    Properties
    {
        [Header(Base Colors)]
        _BaseMap("Bark Texture", 2D) = "white" {}
        _BaseColor("Main Tint", Color) = (1, 1, 1, 1) 
        // Par défaut BLANC pour ne pas assombrir
        
        [Header(Gradient)]
        _ColorTop("Gradient Top", Color) = (1, 1, 1, 1)
        _ColorBot("Gradient Bot", Color) = (0.8, 0.7, 0.6, 1)
        _GradientScale("Gradient Scale", Float) = 10.0
        _GradientOffset("Gradient Offset", Float) = 0.0

        [Header(Stylized Lighting)]
        _RampTex("Toon Ramp (Greyscale)", 2D) = "white" {}
        // On change la couleur de l'ombre pour un gris neutre ou un marron foncé par défaut
        _ShadowTint("Shadow Color", Color) = (0.6, 0.5, 0.4, 1) 
        
        [Header(Wind Physics)]
        _WindMultiplier("Bending Strength", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

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

            // --- BUFFERS (Identique) ---
            struct TreeData { float3 position; float4 rotation; float scale; float4 colorTint; };
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<TreeData> _TreeDataBuffer;
                StructuredBuffer<uint> _VisibleInstanceIndices;
            #endif

            float4x4 QuaternionToMatrix(float4 q) {
                float4x4 m = float4x4(
                    1-2*q.y*q.y-2*q.z*q.z, 2*q.x*q.y-2*q.z*q.w,   2*q.x*q.z+2*q.y*q.w,   0,
                    2*q.x*q.y+2*q.z*q.w,   1-2*q.x*q.x-2*q.z*q.z, 2*q.y*q.z-2*q.x*q.w,   0,
                    2*q.x*q.z-2*q.y*q.w,   2*q.y*q.z+2*q.x*q.w,   1-2*q.x*q.x-2*q.y*q.y, 0,
                    0,                     0,                     0,                     1
                );
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

            // Global Wind
            float _GlobalWindStrength;
            float _WindSpeed; float _WindScale; float2 _WindDirection;
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor; 
                half4 _ColorTop; 
                half4 _ColorBot;
                float _WindMultiplier; 
                float _GradientScale; 
                float _GradientOffset; 
                half4 _ShadowTint;
            CBUFFER_END
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // Contient les données PHYSIQUES (Vent, AO)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                
                // --- CORRECTION ---
                // On sépare la couleur visible des données de vent
                float4 instanceColor : COLOR; 
                float aoFactor : TEXCOORD5; 
                // ------------------
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                
                // 1. SETUP INSTANCE
                float4 instanceTint = float4(1,1,1,1);
                float3 treeRootPos = float3(0,0,0);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    uint id = _VisibleInstanceIndices[unity_InstanceID];
                    TreeData data = _TreeDataBuffer[id];
                    instanceTint = data.colorTint;
                    treeRootPos = data.position;
                #else
                    treeRootPos = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                #endif

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = vertexInput.positionWS;
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,1,1,1));
                output.normalWS = normalInput.normalWS;

                // --- 2. WIND PHYSICS ---
                // On utilise input.color.r et .a pour le VENT, pas pour la couleur !
                float time = _Time.y * _WindSpeed;
                float3 proxyPos = float3(treeRootPos.x, output.positionWS.y, treeRootPos.z);
                
                float3 bending = CalculateMainBending(proxyPos, input.color.r, input.color.a, _WindMultiplier, 
                                                      _WindDirection, _WindSpeed, _GlobalWindStrength, 
                                                      _WindMap, sampler_WindMap, _WindScale, time);
                output.positionWS += bending;

                if (input.color.a > 0.1) {
                    float noiseFreq = time * 15.0 + dot(input.positionOS.xyz, float3(12.9898, 78.233, 45.164));
                    float3 localWobble = sin(noiseFreq) * 0.05 * input.color.a * _WindMultiplier;
                    output.positionWS.xyz += localWobble;
                }

                // --- 3. FINALISATION ---
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // --- CORRECTION CLÉ ---
                // On ne passe QUE la teinte de l'instance. 
                // On NE multiplie PAS par input.color (qui contient des données de vent rouges/vertes)
                output.instanceColor = instanceTint; 
                
                // On passe le Canal B (AO) séparément
                output.aoFactor = input.color.b;

                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                // 1. Texture de base et Teinte Globale
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                // 2. Gradient Vertical (Stylisé)
                float heightFactor = saturate((input.positionWS.y + _GradientOffset) / _GradientScale);
                half3 gradient = lerp(_ColorBot.rgb, _ColorTop.rgb, heightFactor);
                
                // --- CALCUL DE LA COULEUR "FLAT" (ALBEDO) ---
                // Formule nettoyée : Texture * Gradient * CouleurInstance * AO * GlobalTint
                half3 albedo = texColor.rgb * gradient * input.instanceColor.rgb * input.aoFactor * _BaseColor.rgb;

                // 3. Lighting Toon Simple
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = dot(normalize(input.normalWS), normalize(mainLight.direction));
                
                // Half-Lambert (Plus doux que Lambert standard)
                float halfLambert = NdotL * 0.5 + 0.5;
                float shadowAtten = mainLight.shadowAttenuation;

                // Calcul de la Rampe (Ombrage)
                // On combine l'orientation (NdotL) et les ombres portées (shadowAtten)
                float rampVal = saturate(halfLambert * shadowAtten);
                
                // On échantillonne la rampe. 
                // Note : Une rampe Toon doit souvent être échantillonnée avec (valeur, 0.5)
                half3 rampSample = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(rampVal, 0.5)).rgb;

                // --- CORRECTION DE L'ÉCLAIRAGE ---
                // Au lieu de multiplier bêtement par une teinte violette, on fait un lerp propre.
                // Si la rampe est blanche (éclairée) -> Couleur Lumière (Soleil)
                // Si la rampe est noire (ombre) -> Couleur Ombre (_ShadowTint)
                
                // On assume que la texture de rampe va de noir (gauche) à blanc (droite)
                float lightIntensity = rampSample.r; // On utilise la luminosité de la rampe
                
                half3 lightColor = lerp(_ShadowTint.rgb, mainLight.color, lightIntensity);

                // --- COMBINAISON FINALE ---
                half3 finalColor = albedo * lightColor;
                
                // Ajout d'un tout petit peu d'ambient pour éviter les noirs absolus
                half3 ambient = SampleSH(normalize(input.normalWS)) * albedo * 0.2;
                
                return half4(finalColor + ambient, 1.0);
            }
            ENDHLSL
        }
    }
}
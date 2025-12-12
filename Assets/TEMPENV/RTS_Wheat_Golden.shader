Shader "Custom/RTS_Wheat_VertexColor"
{
    Properties
    {
        [Header(Main Settings)]
        _TintColor ("Global Tint", Color) = (1, 1, 1, 1)
        
        [Header(Terrain Blending)]
        _TerrainMap ("Terrain Color Map (Top Down)", 2D) = "white" {}
        _TerrainSize ("Terrain Size (XZ)", Vector) = (1000, 1000, 0, 0)
        _TerrainPos ("Terrain Position (XZ)", Vector) = (0, 0, 0, 0)
        _BlendStrength ("Terrain Blend Strength", Range(0, 1)) = 0.5

        [Header(Wind Physics)]
        _WindMultiplier ("Wind Responsiveness", Range(0.0, 5.0)) = 1.2 

        [Header(Wind Visuals)]
        _WaveColor ("Gust Highlight Color", Color) = (1.0, 1.0, 0.8, 1)
        _WaveOpacity ("Gust Visual Strength", Range(0,1)) = 0.3

        [Header(Variation)]
        _MinScale ("Min Scale Variance", Float) = 0.9
        _MaxScale ("Max Scale Variance", Float) = 1.1
        // --- NOUVEAU ---
        _ColorVar ("Random Darken/Lighten", Range(0, 0.5)) = 0.2
        _YOffset ("Mesh Vertical Offset", Float) = 0.0
        // ---------------
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float4> _VisibleInstances;

            // GLOBALS (Wind Manager)
            TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
            float _WindSpeed;
            float _WindScale;
            float2 _WindDirection;
            float _GlobalWindStrength;

            TEXTURE2D(_TerrainMap); SAMPLER(sampler_TerrainMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _WaveColor;
                float4 _TerrainSize;
                float4 _TerrainPos;
                float _BlendStrength;
                float _WaveOpacity;
                float _WindMultiplier;
                float _MinScale;
                float _MaxScale;
                // --- NOUVEAU ---
                float _ColorVar;
                float _YOffset;
                // ---------------
                float _TipFlutter; // Force du tremblement rapide du sommet
                float _Stiffness;  // 0 = Mou (Herbe), 1 = Dur (Maïs)
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float windMask : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float rnd : TEXCOORD5;
            };

Varyings vert(Attributes input)
{
    Varyings output;

    // --- 1. SETUP INSTANCE (Identique à votre code) ---
    float4 instanceData = _VisibleInstances[input.instanceID];
    float3 instancePos = instanceData.xyz;
    float rnd = instanceData.w;

    // Rotation & Scale
    float angle = rnd * 6.283185;
    float s, c;
    sincos(angle, s, c);
    float scale = lerp(_MinScale, _MaxScale, rnd);

    float3 posOS = input.positionOS.xyz * scale;
    
    // Rotation Y manuelle
    float xNew = posOS.x * c - posOS.z * s;
    float zNew = posOS.x * s + posOS.z * c;
    posOS.x = xNew;
    posOS.z = zNew;

    // Position Monde de base
    float3 positionWS = instancePos + posOS;
    positionWS.y += _YOffset;

    // --- 2. CONFIGURATION PIVOT & LONGUEUR ---
    // Pivot au sol (avec l'offset Y pris en compte)
    float3 pivotPos = instancePos + float3(0, _YOffset, 0);
    // On calcule la distance vertex <-> pivot pour la conserver plus tard (Anti-Stretch)
    float origLength = length(positionWS - pivotPos);
    
    // Si on est exactement au pivot, on ne fait rien (optimisation)
    if (origLength < 0.001) {
        output.positionCS = TransformWorldToHClip(positionWS);
        output.uv = input.uv;
        output.color = input.color;
        output.windMask = 0;
        output.positionWS = positionWS;
        output.rnd = rnd;
        return output;
    }

    // --- 3. WIND PHYSICS "NEXT-GEN" ---

    // A. Vent Global (La direction générale)
    float time = _Time.y * _WindSpeed;
    float2 windUV = positionWS.xz * _WindScale - (_WindDirection * time);
    // On échantillonne le bruit (Rafale principale)
    float noise = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
    
    // B. Ambient Sway (Le mouvement "au repos")
    // Crée une ondulation douce même sans vent fort.
    // On utilise instancePos pour désynchroniser les plantes.
    float ambientFreq = _Time.y * 0.5; // Lent
    float ambientSway = sin(ambientFreq + instancePos.x * 0.3 + instancePos.z * 0.3) * 0.05;

    // C. High Frequency Shiver (Le tremblement des feuilles/épis)
    // Très rapide, très petit, affecte surtout le sommet.
    float shiverFreq = _Time.y * 12.0; // Rapide
    float shiver = sin(shiverFreq + instancePos.x) * _TipFlutter * noise; // Le bruit module le shiver (plus de vent = plus de tremblement)

    // --- 4. CALCUL DE LA FORCE ---
    
    // Masque de hauteur (Quadratique pour une courbure naturelle)
    float heightMask = pow(input.uv.y, 2.0); 
    
    // Pour le maïs/blé, le sommet bouge plus "violemment" par rapport à la tige rigide
    // On ajoute le "Shiver" uniquement sur la partie haute (UV > 0.6)
    float tipMask = max(0, input.uv.y - 0.6);
    
    // Mix des forces
    // Force Principale = (Rafale + Ambient) * ForceGlobale * MultiplicateurLocal
    float mainWindForce = (noise + ambientSway) * _GlobalWindStrength * _WindMultiplier;
    
    // Application de la rigidité (_Stiffness)
    // Plus c'est rigide, moins ça plie, mais le "Shiver" reste.
    mainWindForce *= (1.0 - _Stiffness * 0.5); 

    // Calcul du vecteur de déplacement
    float3 displacement = 0;
    
    // Déplacement XZ (Pliage de la tige)
    displacement.x = _WindDirection.x * mainWindForce * heightMask;
    displacement.z = _WindDirection.y * mainWindForce * heightMask;

    // Ajout du "Shiver" (Tremblement) au sommet (Ajoute du chaos local)
    displacement.x += shiver * tipMask * 0.5;
    displacement.z += shiver * tipMask * 0.5;

    // Position temporaire "étirée"
    float3 positionWithWind = positionWS + displacement;

    // --- 5. LENGTH LOCKING (CRITIQUE) ---
    // C'est votre code existant, essentiel pour ne pas étirer la texture.
    float3 pivotToNew = positionWithWind - pivotPos;
    positionWithWind = pivotPos + normalize(pivotToNew) * origLength;

    // Mise à jour finale
    positionWS = positionWithWind;

    // --- SORTIE ---
    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = input.uv;
    output.color = input.color;
    // On passe le bruit pour le frag shader (Highlighter les rafales)
    output.windMask = noise * _GlobalWindStrength;
    output.positionWS = positionWS;
    output.rnd = rnd;

    return output;
}

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Base Color = Vertex Color * Tint
                float4 baseColor = input.color * _TintColor;

                // --- NOUVEAU : VARIATION DE LUMINOSITÉ ALÉATOIRE ---
                // input.rnd est unique par brin (vient du buffer Compute)
                float brightness = lerp(1.0 - _ColorVar, 1.0 + _ColorVar, input.rnd);
                baseColor.rgb *= brightness;
                // ---------------------------------------------------

                // 2. Terrain Blending
                float2 terrainUV = (input.positionWS.xz - _TerrainPos.xz) / _TerrainSize.xz;
                float4 groundColor = SAMPLE_TEXTURE2D(_TerrainMap, sampler_TerrainMap, terrainUV);
                float blendFactor = (1.0 - input.uv.y) * _BlendStrength; 
                baseColor = lerp(baseColor, groundColor, blendFactor * 0.5);

                // 3. Lighting
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;
                baseColor.rgb *= (mainLight.color * shadow);

                // 4. Wind Highlights
                float highlight = input.windMask * input.uv.y * _WaveOpacity * shadow;
                baseColor = lerp(baseColor, _WaveColor, highlight);

                return baseColor;
            }
            ENDHLSL
        }
    }
}
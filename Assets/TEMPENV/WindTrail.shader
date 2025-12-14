Shader "Custom/ProceduralWindTrail"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _LineThickness("Thickness", Range(1.0, 20.0)) = 5.0
        _SegmentCount("Dash Frequency", Float) = 3.0
        
        _WaveFrequency("Wave Frequency", Float) = 10.0
        _WaveAmplitude("Wave Amplitude", Range(0.0, 0.5)) = 0.1
        _WaveSpeed("Wave Speed", Float) = 10.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha One // Additive
        ZWrite Off
        Cull Off 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; 
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _LineThickness;
                float _SegmentCount;
                float _WaveFrequency;
                float _WaveAmplitude;
                float _WaveSpeed;
            CBUFFER_END

            float _GlobalWindStrength;
            float _WindSpeed;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- 1. WAVE DISTORTION (L'ASTUCE DU SERPENT) ---
                // On calcule une onde sinusoïdale basée sur la longueur de la traînée (UV.x)
                // Cela déforme la coordonnée Y virtuelle.
                
                float timeScroll = _Time.y * _WaveSpeed * _WindSpeed; // Vitesse synchro avec le vent global
                float sineWave = sin((IN.uv.x * _WaveFrequency) + timeScroll);
                
                // On applique l'onde. Le 0.5 est le centre de la particule.
                // On décale le point qu'on est en train de lire vers le haut ou le bas.
                float distortedY = IN.uv.y + (sineWave * _WaveAmplitude);

                // --- 2. DESSIN DE LA LIGNE ---
                // On dessine la ligne basée sur ce Y déformé
                float distFromCenter = abs(distortedY - 0.5) * 2.0;
                
                // Si on sort du cadre à cause de la vague, on coupe (clip)
                if(distFromCenter > 1.0) discard;

                float lineShape = 1.0 - distFromCenter;
                lineShape = pow(lineShape, _LineThickness);

                // --- 3. DASHES / SEGMENTS (MOUVEMENT HORIZONTAL) ---
                float horizontalScroll = _Time.y * _WindSpeed * 5.0; 
                float windGusts = sin((IN.uv.x * _SegmentCount) - horizontalScroll);
                windGusts = smoothstep(-0.5, 1.0, windGusts); 

                // --- 4. FADE BORDS ---
                float fadeTips = sin(IN.uv.x * 3.14159);
                fadeTips = pow(fadeTips, 0.5); // Adoucir le fade

                // --- COMBINAISON ---
                float alpha = lineShape * windGusts * fadeTips;
                
                half4 finalColor = _Color * IN.color;
                finalColor.a *= alpha * saturate(_GlobalWindStrength);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
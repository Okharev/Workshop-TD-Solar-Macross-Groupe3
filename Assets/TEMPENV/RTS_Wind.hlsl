// FICHIER: RTS_Wind.hlsl
#ifndef RTS_WIND_INCLUDED
#define RTS_WIND_INCLUDED

// Cette fonction calcule "combien" le vent souffle à une position donnée
float GetWindStrength(float3 positionWS, float time, float speed, float scale, Texture2D windMap, SamplerState samplerWind)
{
    // 1. Calcul des UVs du vent basés sur la position Monde et le Temps
    float2 windUV = positionWS.xz * scale + (float2(time, time) * speed);
    
    // 2. Lecture de la texture de bruit
    // On utilise SAMPLE_TEXTURE2D_LOD pour pouvoir l'utiliser dans le Vertex Shader
    float noise = SAMPLE_TEXTURE2D_LOD(windMap, samplerWind, windUV, 0).r;
    
    return noise;
}

// Cette fonction applique une rotation RIGIDE (pour les fleurs)
// Elle modifie la position locale (posOS) directement
void ApplyRigidRotation(inout float3 posOS, float windStrength, float bendFactor, float heightMask)
{
    // L'angle dépend de la force du vent et de la flexibilité (bendFactor)
    float angle = windStrength * bendFactor;

    // Rotation autour de l'axe X local (pour pencher en avant/arrière)
    // On pivote autour de la base (y=0)
    float s, c;
    sincos(angle, s, c);
    
    // Rotation classique
    float3 bentPos = posOS;
    bentPos.y = posOS.y * c - posOS.z * s;
    bentPos.z = posOS.y * s + posOS.z * c;

    // On applique le résultat
    posOS = bentPos;
}

#endif
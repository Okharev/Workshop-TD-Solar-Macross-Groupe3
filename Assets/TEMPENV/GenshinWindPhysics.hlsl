#ifndef GENSHIN_WIND_PHYSICS_INCLUDED
#define GENSHIN_WIND_PHYSICS_INCLUDED

// Calculates the main sway used by BOTH Trunk and Leaves to ensure they stay attached.
float3 CalculateMainBending(float3 positionWS, float windWeight, float fragility, float windMultiplier, 
                            float2 windDir, float windSpeed, float globalWindStrength, 
                            TEXTURE2D(windMap), SAMPLER(samplerWindMap), float windScale, float time)
{
    // 1. Sample Global Wind Texture
    float2 windUV = positionWS.xz * windScale - (windDir * time);
    float noise = SAMPLE_TEXTURE2D_LOD(windMap, samplerWindMap, windUV, 0).r;
    float gust = noise * noise; 

    // 2. Ambient Sway (Desynchronized based on World Position)
    float swayTime = time + (positionWS.x + positionWS.z) * 0.5;
    float ambientSway = sin(swayTime) * 0.1;

    // 3. Fragility Boost (Alpha Channel)
    // Branches (Alpha=1) bend twice as much as the main trunk (Alpha=0)
    float branchBoost = 1.0 + (fragility * 2.0);

    // 4. Total Force
    float totalPush = (gust * globalWindStrength + ambientSway) * windMultiplier * windWeight * branchBoost;

    // 5. Displacement with Lagrangian correction (Downward arc)
    float3 displacement = float3(windDir.x * totalPush, 0, windDir.y * totalPush);
    displacement.y -= totalPush * totalPush * 0.2;

    return displacement;
}

// Calculates high-frequency vibration for Leaves only.
float3 CalculateLeafFlutter(float3 positionOS, float3 normalWS, float flutterIntensity, 
                            float time, float branchStiffness)
{
    // High frequency vibration based on object space position
    float flutterFreq = time * 15.0 + dot(positionOS, float3(10,10,10));
    
    // branchStiffness (Green Channel) determines if this vertex allows fluttering
    float flutter = sin(flutterFreq) * flutterIntensity * branchStiffness;
    
    return normalWS * flutter;
}

#endif
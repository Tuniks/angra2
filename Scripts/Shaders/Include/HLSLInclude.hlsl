#ifndef AUXTERRAINFUNCTIONS_INCLUDED
#define AUXTERRAINFUNCTIONS_INCLUDED

const static float epsilon = 0.0001;

// angra biome
const static float baseStartHeights[4] = {0, 0.47, 0.635, 0.635};
const static float baseBlends[4] = {0.28, 0.08, 0.001, 0.001};

// dunes biome
const static float baseDuneStartHeights[3] = {0, 0.47, 0.635};

// mountains biome
const static float baseMountainStartHeights[5] = {0, 0.47, 0.635, 0.76, 0.95};


// ha long biome 

float inverseLerp(float a, float b, float value) {
    return saturate((value-a)/(b-a));
}

void TextureGradient_float(float3 waterTex, float3 sandTex, float3 grassTex, float3 rockTex, float heightPercent, out float3 Out){
    float3 albedo = {0,0,0};

    float drawStrength = inverseLerp(-baseBlends[0]/2 - epsilon, baseBlends[0]/2, heightPercent - baseStartHeights[0]);
    albedo = albedo * (1-drawStrength) + (waterTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[1]/2 - epsilon, baseBlends[1]/2, heightPercent - baseStartHeights[1]);
    albedo = albedo * (1-drawStrength) + (sandTex) * drawStrength;

    // drawStrength = inverseLerp(-baseBlends[2]/2 - epsilon, baseBlends[2]/2, heightPercent - baseStartHeights[2]);
    // albedo = albedo * (1-drawStrength) + (grassTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[3]/2 - epsilon, baseBlends[3]/2, heightPercent - baseStartHeights[3]);
    albedo = albedo * (1-drawStrength) + (rockTex) * drawStrength;

    Out = albedo;
}

void TextureGradientDunes_float(float3 waterTex, float3 sandTex, float3 grassTex, float heightPercent, out float3 Out){
    float3 albedo = {0,0,0};

    float drawStrength = inverseLerp(-baseBlends[0]/2 - epsilon, baseBlends[0]/2, heightPercent - baseDuneStartHeights[0]);
    albedo = albedo * (1-drawStrength) + (waterTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[1]/2 - epsilon, baseBlends[1]/2, heightPercent - baseDuneStartHeights[1]);
    albedo = albedo * (1-drawStrength) + (grassTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[2]/2 - epsilon, baseBlends[2]/2, heightPercent - baseDuneStartHeights[2]);
    albedo = albedo * (1-drawStrength) + (sandTex) * drawStrength;

    Out = albedo;
}

void TextureGradientMountains_float(float3 waterTex, float3 sandTex, float3 rockTex, float3 snowTex, float3 snowTopTex, float heightPercent, out float3 Out){
    float3 albedo = {0,0,0};

    float drawStrength = inverseLerp(-baseBlends[0]/2 - epsilon, baseBlends[0]/2, heightPercent - baseMountainStartHeights[0]);
    albedo = albedo * (1-drawStrength) + (waterTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[1]/2 - epsilon, baseBlends[1]/2, heightPercent - baseMountainStartHeights[1]);
    albedo = albedo * (1-drawStrength) + (sandTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[2]/2 - epsilon, baseBlends[2]/2, heightPercent - baseMountainStartHeights[2]);
    albedo = albedo * (1-drawStrength) + (rockTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[3]/2 - epsilon, baseBlends[3]/2, heightPercent - baseMountainStartHeights[3]);
    albedo = albedo * (1-drawStrength) + (snowTex) * drawStrength;

    drawStrength = inverseLerp(-baseBlends[3]/2 - epsilon, baseBlends[3]/2, heightPercent - baseMountainStartHeights[4]);
    albedo = albedo * (1-drawStrength) + (snowTopTex) * drawStrength;

    Out = albedo;
}

#endif
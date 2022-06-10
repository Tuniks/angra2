#ifndef SPECULARLIGHT_INCLUDED
#define SPECULARLIGHT_INCLUDED

void SpecularLight_half(half3 Specular, half Smoothness, half3 LightDirection, half3 Color, half3 WorldNormal, half3 WorldView, out half3 Out) {
    #ifdef SHADERGRAPH_PREVIEW
        Out = 0;
    #else
        Smoothness = exp2(10 * Smoothness + 1);
        WorldNormal = normalize(WorldNormal);
        WorldView = SafeNormalize(WorldView);
        Out = LightingSpecular(Color, LightDirection, WorldNormal, WorldView, half4(Specular, 0), Smoothness);
    #endif
}

#endif
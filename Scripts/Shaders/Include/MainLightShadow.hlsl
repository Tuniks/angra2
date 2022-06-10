#ifndef MAINLIGHTSHADOW_INCLUDED
#define MAINLIGHTSHADOW_INCLUDED

void MainLight_half(half3 WorldPos, out half3 Direction, out half3 Color, out half DistanceAtten, out half ShadowAtten){
    #ifdef SHADERGRAPH_PREVIEW
		Direction = normalize(half3(1,1,-0.4));
		Color = half4(1,1,1,1);
		DistanceAtten = 1;
        ShadowAtten = 1;
	#else
        half4 shadowCoord = TransformWorldToShadowCoord(WorldPos);

        #if VERSION_GREATER_EQUAL(10, 1)
			ShadowAtten = MainLightShadow(shadowCoord, WorldPos, half4(1,1,1,1), _MainLightOcclusionProbes);
		#else
			ShadowAtten = MainLightRealtimeShadow(shadowCoord);
		#endif

        ShadowAtten = ShadowAtten;

		Light mainLight = GetMainLight(shadowCoord);
		Direction = mainLight.direction;
		Color = mainLight.color;
		DistanceAtten = mainLight.distanceAttenuation;
	#endif
}

#endif
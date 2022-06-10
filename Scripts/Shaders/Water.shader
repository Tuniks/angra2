Shader "Unlit/Water"
{
    Properties
    {	
        _DepthGradientShallow("Depth Gradient Shallow", Color) = (0.325, 0.807, 0.971, 0.725)
        _DepthGradientDeep("Depth Gradient Deep", Color) = (0.086, 0.407, 1, 0.749)
        _DepthMaxDistance("Depth Maximum Distance", Float) = 1
        _SurfaceNoise("Noise Texture", 2D) = "white" {}
        _SurfaceNoiseCutoff("Surface Noise Cutoff", Range(0,1)) = 0.777
        _FoamDistance("Foam Distance", Float) = 0.4
        _SurfaceNoiseScroll("Surface Noise Scroll Amount", Vector) = (0.03, 0.03, 0, 0)

    }

    SubShader
    {
        Pass
        {
			CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPosition : TEXCOORD2;
                float2 noiseUV : TEXCOORD0;
            };

            float4 _DepthGradientShallow;
            float4 _DepthGradientDeep;
            float _DepthMaxDistance;
            sampler2D _CameraDepthTexture;
            
            sampler2D _SurfaceNoise;
            float4 _SurfaceNoise_ST;
            float _SurfaceNoiseCutoff;
            float _FoamDistance;
            float2 _SurfaceNoiseScroll;

            v2f vert (appdata v)
            {
                v2f output;

                output.vertex = UnityObjectToClipPos(v.vertex);
                output.screenPosition = ComputeScreenPos(output.vertex); // Calculando do espaço pra tela
                
                output.noiseUV = TRANSFORM_TEX(v.uv, _SurfaceNoise);

                return output;
            }

            float4 frag (v2f input) : SV_Target
            {
                float existingDepth = tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(input.screenPosition)).r;
                float existingDepthLinear = LinearEyeDepth(existingDepth);

                float depthDifference = existingDepthLinear - input.screenPosition.w;
                float waterDepthDifference = saturate(depthDifference/_DepthMaxDistance);
                float4 waterColor = lerp(_DepthGradientShallow, _DepthGradientDeep, waterDepthDifference);

                float foamDepthDifference = saturate(depthDifference / _FoamDistance);
                float surfaceNoiseCutoff = foamDepthDifference * _SurfaceNoiseCutoff;

                float2 noiseUV = float2(input.noiseUV.x + _Time.y * _SurfaceNoiseScroll.x,
                    input.noiseUV.y + _Time.y * _SurfaceNoiseScroll.y);

                float surfaceNoiseSample = tex2D(_SurfaceNoise, noiseUV).r;
                float surfaceNoise = surfaceNoiseSample > surfaceNoiseCutoff ? 1 : 0;

				return waterColor + surfaceNoise;
            }
            ENDCG
        }
    }
}

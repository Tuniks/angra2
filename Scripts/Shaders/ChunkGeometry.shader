Shader "Custom/ChunkGeometry"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct v2f
            {
                // float2 uv : TEXCOORD0;
                // UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            // Buffers from Compute Buffers
            StructuredBuffer<float3> vertices;
            StructuredBuffer<int> triangles;

            // Shader Properties
            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (uint vertexID: SV_VertexID) {
                v2f o;

                int vertexIndex = triangles[vertexID];
                float3 vertPos = vertices[vertexIndex];

                o.vertex = UnityObjectToClipPos(vertPos);
                // o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                // sample the texture
                half4 customColor = half4(0.5, 0, 0, 1);
                // apply fog
                // UNITY_APPLY_FOG(i.fogCoord, col);
                return customColor;
            }
            ENDCG
        }
    }
}

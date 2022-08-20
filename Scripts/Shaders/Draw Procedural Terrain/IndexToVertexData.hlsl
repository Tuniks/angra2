// Buffers from Compute Buffers
StructuredBuffer<float3> vertices;
StructuredBuffer<float3> normals;
StructuredBuffer<int> triangles;

void IndexToVertexData_float(uint vertexID, out float3 positionOS, out float3 normalOS){
    int vertexIndex = triangles[vertexID];
    positionOS = vertices[vertexIndex];
    normalOS = normals[vertexIndex];
}
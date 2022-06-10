using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;

public class SharedMCMesh {
    private int cases_kernel;
    private int vertices_kernel;
    private int triangles_kernel;

    ComputeShader cs;

    ComputeBuffer cases;
    ComputeBuffer vertexBuffer;
    ComputeBuffer trianglesBuffer;
    ComputeBuffer normalBuffer;

    ComputeBuffer casesCounter;
    ComputeBuffer triangleCounter;
    ComputeBuffer vertexCounter;
    ComputeBuffer vertexIndexCounter;
    ComputeBuffer currentIndexCounter;

    RenderTexture vertexIndexVol;

    private int noThreadsCases = 8;
    private int noThreadsVertex = 512;

    public Mesh CreateMesh(RenderTexture mapData, int meshSize, float meshScale, float surfaceLevel, int lod, ComputeShader _cs){
        // Setting up de compute shader and grabbing kernel IDs
        cs = _cs;
        cases_kernel = cs.FindKernel("ListCases");
        vertices_kernel = cs.FindKernel("GenVertices");
        triangles_kernel = cs.FindKernel("GenTriangles");

        // Create known length buffers
        CreateBuffers(meshSize);

        // Setting basic variables
        cs.SetInt("gridSize", meshSize);
        cs.SetInt("lod", lod);
        cs.SetFloat("surfaceLevel", surfaceLevel);

        // Setting buffers for the list cases kernel
        cases.SetCounterValue(0);
        cs.SetBuffer(cases_kernel, "cases", cases);
        cs.SetBuffer(cases_kernel, "triangleCounter", triangleCounter);
        cs.SetBuffer(cases_kernel, "vertexCounter", vertexCounter);
        cs.SetTexture(cases_kernel, "points", mapData);

        // Dispatching first kernel and retrieving data
        int threadGroups = Mathf.CeilToInt((meshSize)/ (float) noThreadsCases);
        cs.Dispatch(cases_kernel, threadGroups, threadGroups, threadGroups);
        uint[] triCount = {0};
        uint[] vertCount = {0};
        uint[] caseCount = {0};
        triangleCounter.GetData(triCount);
        vertexCounter.GetData(vertCount);
        ComputeBuffer.CopyCount(cases, casesCounter, 0);
        casesCounter.GetData(caseCount);

        // Allocating new buffers and releasing old ones
        triangleCounter.Release();
        vertexCounter.Release();
        if(vertCount[0] == 0){
            ReleaseBuffers();
            Debug.Log("no vertices");
            return new Mesh();
        }

        vertexBuffer = new ComputeBuffer((int)vertCount[0], sizeof(float) * 3, ComputeBufferType.Default);
        normalBuffer = new ComputeBuffer((int)vertCount[0], sizeof(float) * 3, ComputeBufferType.Default);

        // Setting buffers for the generate vertices kernel
        cs.SetBuffer(vertices_kernel, "inCases", cases);
        cs.SetBuffer(vertices_kernel, "casesCount", casesCounter);
        cs.SetBuffer(vertices_kernel, "vertexBuffer", vertexBuffer);
        cs.SetBuffer(vertices_kernel, "vertexIndexCounter", vertexIndexCounter);
        cs.SetTexture(vertices_kernel, "points", mapData);
        cs.SetTexture(vertices_kernel, "vertexIndexVol", vertexIndexVol);
        cs.SetTexture(triangles_kernel, "indexVol", vertexIndexVol);
        cs.SetBuffer(vertices_kernel, "normalBuffer", normalBuffer);

        // Dispatching second kernel and retrieving data
        threadGroups = Mathf.CeilToInt((caseCount[0])/ (float) noThreadsVertex);
        cs.Dispatch(vertices_kernel, threadGroups, 1, 1);
        Vector3[] vertices = new Vector3[vertCount[0]];
        vertexBuffer.GetData(vertices);
        // Vector3[] normals = new Vector3[vertCount[0]];
        // normalBuffer.GetData(normals);

        // Allocating new buffers and releasing old ones
        mapData.Release();
        vertexBuffer.Release();
        normalBuffer.Release();
        if(triCount[0] == 0){
            ReleaseBuffers();
            return null;
        }
        trianglesBuffer = new ComputeBuffer((int)triCount[0] * 3, sizeof(int), ComputeBufferType.Default);

        // Setting buffers for the triangles kernel
        cs.SetBuffer(triangles_kernel, "inCases", cases);
        cs.SetBuffer(triangles_kernel, "casesCount", casesCounter);
        cs.SetBuffer(triangles_kernel, "currentIndexCounter", currentIndexCounter);
        cs.SetBuffer(triangles_kernel, "triangles", trianglesBuffer);

        // Dispatching last buffer
        cs.Dispatch(triangles_kernel, threadGroups, 1, 1);
        int[] triangles = new int[triCount[0] * 3];
        trianglesBuffer.GetData(triangles);

        // Releasing buffers
        ReleaseBuffers();

        // Creating Mesh
        Mesh mesh = new Mesh();
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    void CreateBuffers(int meshSize){
        vertexIndexVol = new RenderTexture(meshSize+1, meshSize+1, 0, RenderTextureFormat.ARGBInt);
        vertexIndexVol.enableRandomWrite = true;
        vertexIndexVol.dimension = TextureDimension.Tex3D;
        vertexIndexVol.volumeDepth = meshSize+1;
        vertexIndexVol.Create();

        int maxCases = (meshSize) * (meshSize) * (meshSize);
        cases = new ComputeBuffer(maxCases, sizeof(float), ComputeBufferType.Append);
        triangleCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        vertexCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        casesCounter = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        vertexIndexCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);
        currentIndexCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Default);

        uint[] triCount = {0};
        triangleCounter.SetData(triCount);
        vertexIndexCounter.SetData(triCount);
        currentIndexCounter.SetData(triCount);
        vertexCounter.SetData(triCount);
    }

    void ReleaseBuffers(){
        cases.Release();
        // vertexBuffer.Release();
        casesCounter.Release();
        vertexIndexCounter.Release();
        currentIndexCounter.Release();
        vertexIndexVol.Release();
        if(trianglesBuffer != null){
            trianglesBuffer.Release();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawChunk : MonoBehaviour {
    public ComputeShader cs;
    public ComputeShader DensityNoiseTextureShader;
    public BiomeDensityData[] biomeDensityData;
    
    private int noThreadsCases = 8;
    private int noThreadsVertex = 512;

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

    public const int chunkSize = 205;
    public const float chunkScale = 15f;
    public float surfaceLevel;
    public Vector2[] noiseParameters;
    public Vector2 chunkOffset;
    public int lod = 1;

    public Material material;
    Bounds bounds;
    int triangleCount = 0;


    void Start() {
        int relativeChunkSize = Mathf.CeilToInt((float) chunkSize / lod);
        int pointCount = relativeChunkSize * relativeChunkSize * relativeChunkSize;

        RenderTexture densityTexture = CreateTextureBuffer(relativeChunkSize);
        GenerateMapDensity(Vector2.zero + chunkOffset, lod, relativeChunkSize, densityTexture);

        DispatchMarchingCubesShader(densityTexture, relativeChunkSize);

        material.SetBuffer("vertices", vertexBuffer);
        material.SetBuffer("triangles", trianglesBuffer);
        material.SetBuffer("normals", normalBuffer);

        float halfSize = chunkSize/2;
        Vector3 boundsCenter = new Vector3(chunkOffset.x + halfSize, halfSize, chunkOffset.y + halfSize);
        bounds = new Bounds(boundsCenter, new Vector3(chunkSize, chunkSize, chunkSize));
        
        Debug.Log(triangleCount);
    }

    void Update(){
        Graphics.DrawProcedural(material, bounds, MeshTopology.Triangles, triangleCount);
    }

    void OnDestroy(){
        ReleaseBuffers();
    }

    void GenerateMapDensity(Vector2 center, int lod, int chunkSize, RenderTexture mapData){
        DensityGenerator densityGenerator = new DensityGenerator();
        densityGenerator.GenerateMapDensityTexture(mapData, chunkSize, chunkScale, lod, biomeDensityData, center, DensityNoiseTextureShader, new Vector4(0,0,-128.39f,0));
    }

    void DispatchMarchingCubesShader(RenderTexture mapDensity, int meshSize){
        int cases_kernel = cs.FindKernel("ListCases");
        int vertices_kernel = cs.FindKernel("GenVertices");
        int triangles_kernel = cs.FindKernel("GenTriangles");

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
        cs.SetTexture(cases_kernel, "points", mapDensity);

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
        vertexBuffer = new ComputeBuffer((int)vertCount[0], sizeof(float) * 3, ComputeBufferType.Default);
        normalBuffer = new ComputeBuffer((int)vertCount[0], sizeof(float) * 3, ComputeBufferType.Default);

        // Setting buffers for the generate vertices kernel
        cs.SetBuffer(vertices_kernel, "inCases", cases);
        cs.SetBuffer(vertices_kernel, "casesCount", casesCounter);
        cs.SetBuffer(vertices_kernel, "vertexBuffer", vertexBuffer);
        cs.SetBuffer(vertices_kernel, "normalBuffer", normalBuffer);
        cs.SetBuffer(vertices_kernel, "vertexIndexCounter", vertexIndexCounter);
        cs.SetTexture(vertices_kernel, "points", mapDensity);
        cs.SetTexture(vertices_kernel, "vertexIndexVol", vertexIndexVol);
        cs.SetTexture(triangles_kernel, "indexVol", vertexIndexVol);

        // Dispatching second kernel and retrieving data
        threadGroups = Mathf.CeilToInt((caseCount[0])/ (float) noThreadsVertex);
        cs.Dispatch(vertices_kernel, threadGroups, 1, 1);
        
        // Allocating new buffers and releasing old ones
        mapDensity.Release();
        trianglesBuffer = new ComputeBuffer((int)triCount[0] * 3, sizeof(int), ComputeBufferType.Default);
    
        // Setting buffers for the triangles kernel
        cs.SetBuffer(triangles_kernel, "inCases", cases);
        cs.SetBuffer(triangles_kernel, "casesCount", casesCounter);
        cs.SetBuffer(triangles_kernel, "currentIndexCounter", currentIndexCounter);
        cs.SetBuffer(triangles_kernel, "triangles", trianglesBuffer);

        // Dispatching last buffer
        cs.Dispatch(triangles_kernel, threadGroups, 1, 1);

        triangleCount = (int) (3 * triCount[0]);

        // Vector3[] vertices = new Vector3[vertCount[0]];
        // int[] triangles = new int[triCount[0] * 3];
        // trianglesBuffer.GetData(triangles);
        // vertexBuffer.GetData(vertices);
    }

    void CreateBuffers(int meshSize){
        vertexIndexVol = new RenderTexture(meshSize+2, meshSize+2, 0, RenderTextureFormat.ARGBInt);
        vertexIndexVol.enableRandomWrite = true;
        vertexIndexVol.dimension = TextureDimension.Tex3D;
        vertexIndexVol.volumeDepth = meshSize+2;
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

    RenderTexture CreateTextureBuffer(int meshSize){
        RenderTexture tex = new RenderTexture(meshSize+1, meshSize+1, 0, RenderTextureFormat.ARGBInt);
        tex.enableRandomWrite = true;
        tex.dimension = TextureDimension.Tex3D;
        tex.volumeDepth = meshSize+1;
        tex.Create();
        return tex;
    }

    void ReleaseBuffers(){
        cases.Release();
        trianglesBuffer.Release();
        casesCounter.Release();
        vertexIndexCounter.Release();
        currentIndexCounter.Release();
        vertexIndexVol.Release();
        vertexBuffer.Release();
        normalBuffer.Release();
    }
    
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawChunk : MonoBehaviour {
    public ComputeShader cs;
    public ComputeShader DensityNoiseTextureShader;
    public BiomeDensityData[] biomeDensityData;
    private Terraformer terraformer;

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

    private int chunkSize;
    private float chunkScale;
    private float surfaceLevel;
    public int lod = 1;
    private Vector3 chunkOffset;
    public Vector3 chunkID;

    public Material[] materials;
    private Material material;
    private int biomeID = 0;
    public Bounds bounds;
    private int triangleCount = 0;


    void Start() {
        chunkSize = TerrainData.chunkSize + 4;
    	chunkScale = TerrainData.scale;
        surfaceLevel = TerrainData.surfaceLevel;

        float halfSize = chunkSize/2;
        Vector3 boundsCenter = new Vector3(chunkOffset.x + halfSize, chunkOffset.y + halfSize, chunkOffset.z + halfSize);
        bounds = new Bounds(boundsCenter, new Vector3(chunkSize, chunkSize, chunkSize));

        Draw();
    }

    void Update(){
        Graphics.DrawProcedural(material, bounds, MeshTopology.Triangles, triangleCount);
    }

    void OnDisable(){
        ReleaseBuffers();
    }

    public void Initialize(Vector3 _offset, Vector3 _id, int _lod){
        chunkOffset = _offset;
        chunkID = _id;
        lod = _lod;
        terraformer = GetComponentInParent<Terraformer>();

        if(_offset.y != 0){
            transform.GetChild(0).gameObject.SetActive(false);
        }
    }

    public void UpdateLOD(int newLOD){
        lod = newLOD;
    }

    public void Draw(){
        ReleaseBuffers();
        int relativeChunkSize = Mathf.CeilToInt((float) chunkSize / lod);
        int pointCount = relativeChunkSize * relativeChunkSize * relativeChunkSize;

        RenderTexture densityTexture = CreateTextureBuffer(relativeChunkSize+2);
        GenerateMapDensity(chunkOffset, lod, relativeChunkSize+2, densityTexture);

        DispatchMarchingCubesShader(densityTexture, relativeChunkSize);

    	material = GetMaterial();
        material.SetBuffer("vertices", vertexBuffer);
        material.SetBuffer("triangles", trianglesBuffer);
        material.SetBuffer("normals", normalBuffer);
    }

    void GenerateMapDensity(Vector3 center, int lod, int chunkSize, RenderTexture mapData){
        DensityGenerator densityGenerator = new DensityGenerator();
        biomeID = densityGenerator.GetBiomeKernel(new Vector2(center.x, center.z));

        densityGenerator.GenerateMapDensityTexture(mapData, chunkSize, chunkScale, lod, biomeDensityData, center, terraformer, new Vector2(chunkID.x, chunkID.z), DensityNoiseTextureShader);
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
        cs.SetVector("offset", chunkOffset);

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

        // If there are no cases in the chunk, quit
        if(triCount[0] == 0){
            mapDensity.Release();
            EarlyExit();
            return;
        }

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

        triangleCount = (int) (3 * triCount[0]);

        // Dispatching last buffer
        cs.Dispatch(triangles_kernel, threadGroups, 1, 1);
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
        RenderTexture tex = new RenderTexture(meshSize+1, meshSize+1, 0, RenderTextureFormat.ARGBFloat);
        tex.enableRandomWrite = true;
        tex.dimension = TextureDimension.Tex3D;
        tex.volumeDepth = meshSize+1;
        tex.Create();
        return tex;
    }

    void ReleaseBuffers(){
        if(cases != null) cases.Release();
        if(casesCounter != null) casesCounter.Release();
        if(vertexIndexCounter != null) vertexIndexCounter.Release();
        if(currentIndexCounter != null) currentIndexCounter.Release();
        if(vertexIndexVol != null) vertexIndexVol.Release();
        if(trianglesBuffer != null) trianglesBuffer.Release();
        if(vertexBuffer != null) vertexBuffer.Release();
        if(normalBuffer != null) normalBuffer.Release();
    }
    
    void EarlyExit(){
        triangleCounter.Release();
        vertexCounter.Release();
        ReleaseBuffers();
    }

    Material GetMaterial(){
        return new Material(materials[biomeID]);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

public class AsyncTerrainGeneration : MonoBehaviour {
    public Transform viewer;
    static MapManager mapManager;
    public GameObject waterPrefab;
    
    public Vector2[] detailLevels;
    float maxViewDistance;
    float sqrViewDistance;
    float[] sqrViewDistances;
    public static Vector2 viewerPosition;
    Vector2 oldViewerPosition;
    
    int chunkSize;
    float chunkScale;
    
    int chunksVisibleInViewDistance;

    Dictionary<int, RenderTexture> mapDataTextures = new Dictionary<int, RenderTexture>();

    const float viewerMoveThresholdForChunkUpdate = 10f;
    const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();
    List<Chunk> chunks = new List<Chunk>();
    Queue<Chunk> recyclableChunks = new Queue<Chunk>();

    List<GameObject> waters = new List<GameObject>();
    Queue<GameObject> recyclabeWaters = new Queue<GameObject>();

    public struct ChunkToRender {
        public Chunk chunk;
        public Vector3 position;
        public int verticalChunk;
    }

    protected Queue<ChunkToRender> chunksToRender;
    protected bool isRenderingChunk = false;
    protected AsyncSharedMCMesh meshCreator;

    Camera cam;
    Plane[] planes;

    public AssetPlacer assetPlacer;
    public bool placeObjects;

    void Start() {
        mapManager = FindObjectOfType<MapManager>();
        chunkSize = MapManager.chunkSize - 1;
        chunkScale = mapManager.terrainData.scale;

        meshCreator = new AsyncSharedMCMesh(mapManager.VertexSharingMarchingCubeShader, mapManager.surfaceLevel);

        maxViewDistance = detailLevels[detailLevels.Length-1].y;
        chunksVisibleInViewDistance = Mathf.CeilToInt(maxViewDistance / chunkSize);
        sqrViewDistance = maxViewDistance * maxViewDistance;

        sqrViewDistances = new float[detailLevels.Length];
        for(int i = 0; i < detailLevels.Length; i++){
            sqrViewDistances[i] = detailLevels[i].y * detailLevels[i].y;

            int relativeChunkSize =  Mathf.CeilToInt((float)MapManager.chunkSize / detailLevels[i].x);
            mapDataTextures.Add((int)detailLevels[i].x, mapManager.CreateTextureBuffer(relativeChunkSize));
        }

        chunksToRender = new Queue<ChunkToRender>();
        cam = Camera.main;
    }

    void Update() {
        if(!isRenderingChunk && chunksToRender.Count > 0){
            isRenderingChunk = true;
            ChunkToRender chunk = chunksToRender.Dequeue();
            chunk.chunk.RequestVertices(chunk, meshCreator, mapDataTextures[chunk.chunk.lod], this.SetIsRendering);
        }

        // Dividing viewerposition by scale to avoid multiplying things in relation to the position by scale.
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / chunkScale;

        // Update only when viewer has moved a certain distance since last chunk update
    	if((oldViewerPosition - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
            oldViewerPosition = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void OnDestroy(){
        meshCreator.ReleaseBuffers();
    }

    void UpdateVisibleChunks() {
        for (int i = chunks.Count - 1; i >= 0; i--) {
            Chunk chunk = chunks[i];
            float sqrDist = SqrPlayerDistanceFromCenter(chunk.chunkID);
            if(sqrDist > sqrViewDistance){
                chunkDictionary.Remove(chunk.chunkID);
                recyclableChunks.Enqueue(chunk);
                chunks.RemoveAt(i);

                recyclabeWaters.Enqueue(waters[i]);
                waters.RemoveAt(i);
            }
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        planes = GeometryUtility.CalculateFrustumPlanes(cam);

        for(int y = chunksVisibleInViewDistance; y >= -chunksVisibleInViewDistance; y--) {
            for(int x = chunksVisibleInViewDistance; x >= -chunksVisibleInViewDistance; x--) {
                Vector2 chunkID = new Vector2(currentChunkCoordX + x, currentChunkCoordY + y);
                if(!chunkDictionary.ContainsKey(chunkID)) {
                    float sqrDist = SqrPlayerDistanceFromCenter(chunkID);
                    Bounds bound = CalculateBounds(chunkID);
                    bool insideBounds = GeometryUtility.TestPlanesAABB(planes, bound);
                    if((sqrDist < sqrViewDistance) && insideBounds) {
                        Chunk chunk;
                        GameObject water;
                        bool shouldPlaceAssets = false;
                        int currentLOD = GetLODFromID(chunkID, out shouldPlaceAssets);
                        if(recyclableChunks.Count > 0){
                            chunk = recyclableChunks.Dequeue();
                            chunk.UpdateData(chunkID, chunkSize, currentLOD, (shouldPlaceAssets && placeObjects));
                            water = recyclabeWaters.Dequeue();
                            UpdateWaterPosition(water, chunkID);

                        } else {
                            chunk = new Chunk(chunkID, chunkSize, currentLOD, transform, (shouldPlaceAssets && placeObjects), assetPlacer);
                            water = CreateWater(chunkID, transform);
                        }

                        chunkDictionary.Add(chunkID, chunk);
                        chunks.Add(chunk);
                        waters.Add(water);
                        chunk.UpdateMesh(chunksToRender);
                    }
                } else {
                    Chunk chunk = chunkDictionary[chunkID];
                    bool shouldPlaceAssets;
                    int updatedLOD = GetLODFromID(chunkID, out shouldPlaceAssets); 
                    if (chunk.lod != updatedLOD){
                        chunk.UpdateLOD(updatedLOD, (shouldPlaceAssets && placeObjects));
                        chunk.UpdateMesh(chunksToRender);
                    } else if (chunk.shouldPlaceAssets != shouldPlaceAssets){
                        chunk.UpdateShouldPlaceAssets((shouldPlaceAssets && placeObjects));
                    }
                }
            }
        }
    }

    float SqrPlayerDistanceFromCenter(Vector2 chunkID) {
        Vector2 position = chunkID * chunkSize;
        Vector2 center = position + Vector2.one * chunkSize / 2;
        Vector2 offset = viewerPosition - center;
        return offset.sqrMagnitude; //CHECK
    }

    void UpdateWaterPosition(GameObject water, Vector2 chunkID) {
        water.transform.position = new Vector3(chunkID.x * chunkSize * chunkScale + chunkSize * chunkScale / 2, 1.25f * chunkSize * chunkScale, chunkID.y * chunkSize * chunkScale + chunkSize * chunkScale / 2);
    }

    GameObject CreateWater(Vector2 chunkID, Transform parent) {
        Vector3 position = new Vector3(chunkID.x * chunkSize * chunkScale + chunkSize * chunkScale / 2, 1.25f * chunkSize * chunkScale, chunkID.y * chunkSize * chunkScale + chunkSize * chunkScale / 2);
        GameObject water = Instantiate(waterPrefab, position, Quaternion.identity);
        water.transform.parent = parent;
        return water;
    }

    int GetLODFromID(Vector2 chunkID, out bool isFirstLOD){
        float sqrDist = SqrPlayerDistanceFromCenter(chunkID);
        isFirstLOD = false;

        for(int i = 0; i < detailLevels.Length; i++){
            if(sqrDist < sqrViewDistances[i]){
                if(i == 0) isFirstLOD = true;
                return (int) detailLevels[i].x;
            }
        }

        return (int) detailLevels[detailLevels.Length-1].x;
    }

    protected void SetIsRendering(){
        isRenderingChunk = false;
    }

    Bounds CalculateBounds(Vector2 chunkID){
        int verticalChunks = 3;
        Vector2 pos2d = chunkID * chunkSize *  mapManager.terrainData.scale;
        Vector3 pos3d = new Vector3(pos2d.x, verticalChunks/2 * chunkSize *  mapManager.terrainData.scale, pos2d.y);
        Vector3 lengths = new Vector3(1, verticalChunks/2, 1) * chunkSize *  mapManager.terrainData.scale;
 
        return new Bounds(pos3d, lengths);
    }

    public class Chunk {
        Vector2 position;
        public Vector2 chunkID;
        public int lod;

        GameObject parentObject;
        GameObject[] chunkObjects;
        MeshRenderer[] meshRenderers;
        MeshFilter[] meshFilters;

        MeshCollider meshCollider;
        public bool shouldPlaceAssets = false;

        int verticalChunks = 3;
        AssetPlacer assetPlacer;

        public Chunk(Vector2 chunkID, int size, int lod, Transform parent, bool _shouldPlaceAssets, AssetPlacer _assetPlacer) {
            this.chunkID = chunkID;
            this.lod = lod;
            position = chunkID * size;
            shouldPlaceAssets = _shouldPlaceAssets;
            assetPlacer = _assetPlacer;

            parentObject = new GameObject("Terrain Chunk " + chunkID.ToString());
            chunkObjects = new GameObject[verticalChunks];
            meshRenderers = new MeshRenderer[verticalChunks];
            meshFilters = new MeshFilter[verticalChunks];

            Vector3 position3d = new Vector3(position.x, 0, position.y);
            parentObject.transform.position = position3d * mapManager.terrainData.scale;
            parentObject.transform.parent = parent;
            parentObject.SetActive(false);

            for(int i = 0; i < verticalChunks; i++){
                chunkObjects[i] = new GameObject("chunk " + i.ToString());

                meshRenderers[i] = chunkObjects[i].AddComponent<MeshRenderer>();
                meshFilters[i] = chunkObjects[i].AddComponent<MeshFilter>();
                meshRenderers[i].material = mapManager.GetBiomeMaterial(position);

                position3d.y = i * size;
                chunkObjects[i].transform.position = position3d * mapManager.terrainData.scale;
                chunkObjects[i].transform.parent = parentObject.transform;
                chunkObjects[i].transform.localScale = Vector3.one * mapManager.terrainData.scale;
            }

            meshCollider = chunkObjects[1].AddComponent<MeshCollider>(); // Only collide w middle chunk
        }

        public void UpdateData(Vector2 chunkID, int size, int currentLOD, bool _shouldPlaceAssets) {
            parentObject.name = "Terrain Chunk " + chunkID.ToString();
            parentObject.SetActive(false);
            
            this.chunkID = chunkID;
            position = chunkID * size;
            Vector3 position3d = new Vector3(position.x, 0, position.y);
            parentObject.transform.position = position3d * mapManager.terrainData.scale;
            this.lod = currentLOD;

            shouldPlaceAssets = _shouldPlaceAssets;
            meshCollider.sharedMesh = null;
        }

        public void UpdateLOD(int newLOD, bool should){
            this.lod = newLOD;
            this.shouldPlaceAssets = should;
        }

        public void UpdateShouldPlaceAssets(bool should){
            this.shouldPlaceAssets = should;
            if (should) {
                PlaceAssets();
            }
        }

        public void UpdateMesh(Queue<ChunkToRender> chunksToRender) {
            for(int i = 0; i < verticalChunks; i++){
                Vector3 chunkPos = new Vector3(position.x, i * (MapManager.chunkSize-1), position.y);
                ChunkToRender chunkData;
                chunkData.chunk = this;
                chunkData.position = chunkPos;
                chunkData.verticalChunk = i;
                chunksToRender.Enqueue(chunkData);
                meshRenderers[i].material = mapManager.GetBiomeMaterial(position);
            }
        }

        public void RequestVertices(ChunkToRender chunkToRender, AsyncSharedMCMesh meshCreator, RenderTexture mapDataTexture, Action SetIsRendering){
            int relativeChunkSize =  Mathf.CeilToInt((float)MapManager.chunkSize / this.lod);
            mapManager.GenerateMapDataTextureFrom3DOrigin(chunkToRender.position, this.lod, relativeChunkSize, mapDataTexture);
            bool hasVertices = meshCreator.CreateVertices(mapDataTexture, relativeChunkSize, 1f, this.lod);
            if(!hasVertices){
                BuildMesh(meshCreator, null, null, chunkToRender, SetIsRendering);
                return;
            }

            AsyncGPUReadback.Request(meshCreator.vertexBuffer, (AsyncGPUReadbackRequest request) => GetVerticesCallback(request, meshCreator, mapDataTexture, chunkToRender, SetIsRendering));
        }

        void GetVerticesCallback(AsyncGPUReadbackRequest request, AsyncSharedMCMesh meshCreator, RenderTexture mapDataTexture, ChunkToRender chunkToRender, Action SetIsRendering){
            Vector3[] vertices = new Vector3[meshCreator.verts];
            vertices = request.GetData<Vector3>().ToArray();

            meshCreator.CreateTriangles(mapDataTexture);
            AsyncGPUReadback.Request(meshCreator.trianglesBuffer, (AsyncGPUReadbackRequest request) => GetTrianglesCallback(request, meshCreator, vertices, chunkToRender, SetIsRendering));
        }

        void GetTrianglesCallback(AsyncGPUReadbackRequest request, AsyncSharedMCMesh meshCreator, Vector3[] vertices, ChunkToRender chunkToRender, Action SetIsRendering){
            int[] triangles = new int[meshCreator.tris];
            triangles = request.GetData<int>().ToArray();

            BuildMesh(meshCreator, vertices, triangles, chunkToRender, SetIsRendering);
        }

        void BuildMesh(AsyncSharedMCMesh meshCreator, Vector3[] vertices, int[] triangles, ChunkToRender chunkToRender, Action SetIsRendering){
            meshFilters[chunkToRender.verticalChunk].sharedMesh = meshCreator.GetMesh(vertices, triangles);
            SetIsRendering();
            parentObject.SetActive(true);

            if(this.shouldPlaceAssets){
                PlaceAssets();
            }
        }

        public void PlaceAssets(){
            if(meshFilters[1].sharedMesh == null || meshFilters[1].sharedMesh.vertexCount <= 0){
                return;
            }
            meshCollider.sharedMesh = meshFilters[1].sharedMesh;
            assetPlacer.PlaceAssets(new Vector3(position.x, 0, position.y) * mapManager.terrainData.scale, (MapManager.chunkSize - 1) * mapManager.terrainData.scale);
        }
    }
}

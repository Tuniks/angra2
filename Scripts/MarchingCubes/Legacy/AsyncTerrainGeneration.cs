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
        chunkScale = MapManager.terrainScale;

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
                            chunk = new Chunk(chunkID, chunkSize, currentLOD, transform, (shouldPlaceAssets && placeObjects), assetPlacer, mapManager);
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
        Vector2 pos2d = chunkID * chunkSize * chunkScale;
        Vector3 pos3d = new Vector3(pos2d.x, verticalChunks/2 * chunkSize *  chunkScale, pos2d.y);
        Vector3 lengths = new Vector3(1, verticalChunks/2, 1) * chunkSize *  chunkScale;
 
        return new Bounds(pos3d, lengths);
    }
}

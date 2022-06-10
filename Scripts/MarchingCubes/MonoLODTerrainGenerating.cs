using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonoLODTerrainGenerating : MonoBehaviour {
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

    const float viewerMoveThresholdForChunkUpdate = 50f;
    const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    Dictionary<Vector2, Chunk> chunkDictionary = new Dictionary<Vector2, Chunk>();
    List<Chunk> chunks = new List<Chunk>();
    Queue<Chunk> recyclableChunks = new Queue<Chunk>();

    List<GameObject> waters = new List<GameObject>();
    Queue<GameObject> recyclabeWaters = new Queue<GameObject>();

    void Start() {
        mapManager = FindObjectOfType<MapManager>();
        chunkSize = MapManager.chunkSize - 1;
        chunkScale = mapManager.terrainData.scale;

        maxViewDistance = detailLevels[detailLevels.Length-1].y;
        chunksVisibleInViewDistance = Mathf.CeilToInt(maxViewDistance / chunkSize);
        sqrViewDistance = maxViewDistance * maxViewDistance;

        sqrViewDistances = new float[detailLevels.Length];
        for(int i = 0; i < detailLevels.Length; i++){
            sqrViewDistances[i] = detailLevels[i].y * detailLevels[i].y;

            int relativeChunkSize =  Mathf.CeilToInt((float)MapManager.chunkSize / detailLevels[i].x);
            mapDataTextures.Add((int)detailLevels[i].x, mapManager.CreateTextureBuffer(relativeChunkSize));
        }

        UpdateVisibleChunks();
    }

    void Update() {
        // Dividing viewerposition by scale to avoid multiplying things in relation to the position by scale.
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / chunkScale;

        // Update only when viewer has moved a certain distance since last chunk update
    	if((oldViewerPosition - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
            oldViewerPosition = viewerPosition;
            UpdateVisibleChunks();
        }
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

        for(int y = -chunksVisibleInViewDistance; y <= chunksVisibleInViewDistance; y++) {
            for(int x = -chunksVisibleInViewDistance; x <= chunksVisibleInViewDistance; x++) {
                Vector2 chunkID = new Vector2(currentChunkCoordX + x, currentChunkCoordY + y);

                if(!chunkDictionary.ContainsKey(chunkID)) {
                    float sqrDist = SqrPlayerDistanceFromCenter(chunkID);
                    if(sqrDist < sqrViewDistance) {
                        // POSSIBLE TODO: CHECK IF CHUNK BOUNDS IS IN VIEW OF THE CAMERA
                        Chunk chunk;
                        GameObject water;
                        int currentLOD = GetLODFromID(chunkID);
                        if(recyclableChunks.Count > 0){
                            chunk = recyclableChunks.Dequeue();
                            chunk.UpdateData(chunkID, chunkSize, currentLOD);
                            water = recyclabeWaters.Dequeue();
                            UpdateWaterPosition(water, chunkID);

                        } else {
                            chunk = new Chunk(chunkID, chunkSize, currentLOD, transform);
                            water = CreateWater(chunkID, transform);
                        }

                        chunkDictionary.Add(chunkID, chunk);
                        chunks.Add(chunk);
                        waters.Add(water);
                        chunk.UpdateMesh(this.mapDataTextures[currentLOD]);
                    }
                } else {
                    Chunk chunk = chunkDictionary[chunkID];
                    int updatedLOD = GetLODFromID(chunkID); 
                    if (chunk.lod != updatedLOD){
                        chunk.UpdateLOD(updatedLOD);
                        chunk.UpdateMesh(this.mapDataTextures[updatedLOD]);
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
        water.transform.position = new Vector3(chunkID.x * chunkSize * chunkScale + chunkSize * chunkScale / 2, 635, chunkID.y * chunkSize * chunkScale + chunkSize * chunkScale / 2);
    }

    GameObject CreateWater(Vector2 chunkID, Transform parent) {
        Vector3 position = new Vector3(chunkID.x * chunkSize * chunkScale + chunkSize * chunkScale / 2, 635, chunkID.y * chunkSize * chunkScale + chunkSize * chunkScale / 2);
        GameObject water = Instantiate(waterPrefab, position, Quaternion.identity);
        water.transform.parent = parent;
        return water;
    }

    int GetLODFromID(Vector2 chunkID){
        float sqrDist = SqrPlayerDistanceFromCenter(chunkID);

        for(int i = 0; i < detailLevels.Length-1; i++){
            if(sqrDist < sqrViewDistances[i]){
                return (int) detailLevels[i].x;
            }
        }
        return (int) detailLevels[detailLevels.Length-1].x;
    }

    class Chunk {
        Vector2 position;
        public Vector2 chunkID;
        public int lod;

        GameObject parentObject;
        GameObject[] chunkObjects;
        MeshRenderer[] meshRenderers;
        MeshFilter[] meshFilters;

        int verticalChunks = 3;

        public Chunk(Vector2 chunkID, int size, int lod, Transform parent) {
            position = chunkID * size;
            this.chunkID = chunkID;
            this.lod = lod;

            parentObject = new GameObject("Terrain Chunk " + chunkID.ToString());
            chunkObjects = new GameObject[verticalChunks];
            meshRenderers = new MeshRenderer[verticalChunks];
            meshFilters = new MeshFilter[verticalChunks];

            Vector3 position3d = new Vector3(position.x, 0, position.y);
            parentObject.transform.position = position3d * mapManager.terrainData.scale;
            parentObject.transform.parent = parent;
            
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
        }

        public void UpdateData(Vector2 chunkID, int size, int currentLOD) {
            position = chunkID * size;
            this.chunkID = chunkID;
            Vector3 position3d = new Vector3(position.x, 0, position.y);
            parentObject.transform.position = position3d * mapManager.terrainData.scale;
            this.lod = currentLOD;

            parentObject.name = "Terrain Chunk " + chunkID.ToString();
        }

        public void UpdateMesh(RenderTexture mapDataTexture) {
            for(int i = 0; i < verticalChunks; i++){
                Vector3 chunkPos = new Vector3(position.x, i * (MapManager.chunkSize-1), position.y);
                meshFilters[i].sharedMesh = CreateMesh(chunkPos, mapDataTexture);
                meshRenderers[i].material = mapManager.GetBiomeMaterial(position);
            }
        }

        public void UpdateLOD(int newLOD){
            this.lod = newLOD;
        }

        Mesh CreateMesh(Vector3 offset, RenderTexture mapDataTexture){
            int relativeChunkSize =  Mathf.CeilToInt((float)MapManager.chunkSize / this.lod);
            mapManager.GenerateMapDataTextureFrom3DOrigin(offset, lod, relativeChunkSize, mapDataTexture);
            Mesh mesh = mapManager.GenerateSharedVerticesMesh(mapDataTexture, lod, relativeChunkSize);

            return mesh;
        }
    }
}
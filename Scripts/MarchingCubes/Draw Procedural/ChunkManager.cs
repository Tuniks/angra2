using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ChunkManager : MonoBehaviour{
    public Transform viewer;
    public GameObject chunkPrefab;
    private Camera cam;
    private Plane[] planes;
    
    public Vector2[] detailLevels;
    private int chunkSize;
    private float chunkScale;
    
    private int chunksVisibleInViewDistance;
    private float maxViewDistance;
    private float sqrViewDistance;
    private float[] sqrViewDistances;
    private Vector2 viewerPosition;
    private Vector2 oldViewerPosition;
    private const float viewerMoveThresholdForChunkUpdate = 10f;
    private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    Dictionary<Vector2, DrawChunk> chunkDictionary = new Dictionary<Vector2, DrawChunk>();

    void Start() {
        chunkSize = TerrainData.chunkSize;
        chunkScale = TerrainData.scale;

        maxViewDistance = detailLevels[detailLevels.Length-1].y;
        chunksVisibleInViewDistance = Mathf.CeilToInt(maxViewDistance / chunkSize);
        sqrViewDistance = maxViewDistance * maxViewDistance;

        sqrViewDistances = new float[detailLevels.Length];
        for(int i = 0; i < detailLevels.Length; i++){
            sqrViewDistances[i] = detailLevels[i].y * detailLevels[i].y;
        }

        cam = Camera.main;
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
        foreach(KeyValuePair<Vector2, DrawChunk> c in chunkDictionary.ToList()){
            if(SqrPlayerDistanceFromCenter(c.Key) > sqrViewDistance){
                RemoveChunk(c.Key);
            }
        }

        // for (int i = chunks.Count - 1; i >= 0; i--) {
        //     DrawChunk chunk = chunks[i];
        //     float sqrDist = SqrPlayerDistanceFromCenter(chunk.chunkID);
        //     if(sqrDist > sqrViewDistance){
        //         chunkDictionary.Remove(chunk.chunkID);
        //         chunks.RemoveAt(i);
        //     }
        // }

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
                        int currentLOD = GetLODFromID(chunkID);
                        CreateChunk(chunkID, currentLOD);
                    }
                }
                 else {
                    DrawChunk chunk = chunkDictionary[chunkID];
                    int updatedLOD = GetLODFromID(chunkID); 
                    if (chunk.lod != updatedLOD){
                        RemoveChunk(chunkID);
                        CreateChunk(chunkID, updatedLOD);
                    } 
                }
            }
        }
    }

    void CreateChunk(Vector2 id, int lod){
        Vector3 pos = new Vector3(id.x, 0, id.y) * chunkSize;
        GameObject chunkObject = Instantiate(chunkPrefab, pos, Quaternion.identity);
        chunkObject.name = id.ToString();
        DrawChunk chunk = chunkObject.GetComponent<DrawChunk>();
        chunk.Initialize(id * (chunkSize+1), id, lod);

        chunkDictionary.Add(id, chunk);
    }

    void RemoveChunk(Vector2 id){
        if(!chunkDictionary.ContainsKey(id)) return;

        DrawChunk chunk = chunkDictionary[id];

        Destroy(chunk.gameObject);
        chunkDictionary.Remove(id);
    }

    float SqrPlayerDistanceFromCenter(Vector2 chunkID) {
        Vector2 position = chunkID * chunkSize;
        Vector2 center = position + Vector2.one * chunkSize / 2;
        Vector2 offset = viewerPosition - center;
        return offset.sqrMagnitude; //CHECK
    }

    int GetLODFromID(Vector2 chunkID){
        float sqrDist = SqrPlayerDistanceFromCenter(chunkID);

        for(int i = 0; i < detailLevels.Length; i++){
            if(sqrDist < sqrViewDistances[i]){
                return (int) detailLevels[i].x;
            }
        }

        return (int) detailLevels[detailLevels.Length-1].x;
    }

    Bounds CalculateBounds(Vector2 chunkID){
        int verticalChunks = 3;
        Vector2 pos2d = chunkID * chunkSize * chunkScale;
        Vector3 pos3d = new Vector3(pos2d.x, verticalChunks/2 * chunkSize *  chunkScale, pos2d.y);
        Vector3 lengths = new Vector3(1, verticalChunks/2, 1) * chunkSize *  chunkScale;
 
        return new Bounds(pos3d, lengths);
    }
}

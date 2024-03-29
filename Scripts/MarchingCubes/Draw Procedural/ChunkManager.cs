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
    private const int verticalChunks = 2;
    
    private int chunksVisibleInViewDistance;
    private float maxViewDistance;
    private float sqrViewDistance;
    private float[] sqrViewDistances;
    private Vector2 viewerPosition;
    private Vector2 oldViewerPosition;
    private const float viewerMoveThresholdForChunkUpdate = 10f;
    private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;

    Dictionary<Vector2, DrawChunk[]> chunkDictionary = new Dictionary<Vector2, DrawChunk[]>();

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
        foreach(KeyValuePair<Vector2, DrawChunk[]> c in chunkDictionary.ToList()){
            if(SqrPlayerDistanceFromCenter(c.Key) > sqrViewDistance){
                RemoveChunks(c.Key);
            }
        }

        int currentChunkCoordX = Mathf.FloorToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.FloorToInt(viewerPosition.y / chunkSize);

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
                        CreateChunks(chunkID, currentLOD);
                    }
                } else {
                    DrawChunk chunk = chunkDictionary[chunkID][0];
                    int updatedLOD = GetLODFromID(chunkID); 
                    if (chunk.lod != updatedLOD){
                        UpdateLOD(chunkID, updatedLOD);
                    } 
                }
            }
        }
    }

    void CreateChunks(Vector2 id, int lod){
        DrawChunk[] chunks = new DrawChunk[verticalChunks];

        for(int i = 0; i < verticalChunks; i++){
            chunks[i] = CreateChunk(new Vector3(id.x, i, id.y), lod);
        }
    
        chunkDictionary.Add(id, chunks);
    }

    DrawChunk CreateChunk(Vector3 id, int lod){
        Vector3 pos = id * chunkSize;
        GameObject chunkObject = Instantiate(chunkPrefab, pos, Quaternion.identity);
        chunkObject.name = id.ToString(); 
        chunkObject.transform.parent = this.gameObject.transform;
        DrawChunk chunk = chunkObject.GetComponent<DrawChunk>();
        chunk.Initialize(id * (chunkSize+1), id, lod);

        return chunk;
    }

    void RemoveChunks(Vector2 id){
        if(!chunkDictionary.ContainsKey(id)) return;

        DrawChunk[] chunks = chunkDictionary[id];
        foreach(DrawChunk c in chunks){
            Destroy(c.gameObject);
        }
        chunkDictionary.Remove(id);
    }

    void UpdateLOD(Vector2 id, int lod){
        if(!chunkDictionary.ContainsKey(id)) return;
        DrawChunk[] chunks = chunkDictionary[id];
        foreach(DrawChunk c in chunks){
            c.UpdateLOD(lod);
            c.Draw();
        }
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

    public Vector2[] GetChunksInRange(Vector3 pos, float range){
        List<Vector2> neighbors = new List<Vector2>();
        Vector2 centerID = GetIDFromPosition(new Vector2(pos.x, pos.z));
        neighbors.Add(new Vector2(0,0));

        Vector2 forwardID = GetIDFromPosition(new Vector2(pos.x + (5*range), pos.z + (5*range)));
        Vector2 backwardID = GetIDFromPosition(new Vector2(pos.x - (5*range), pos.z - (5*range)));
        
        if(centerID.x != forwardID.x) neighbors.Add(new Vector2(1,0));
        if(centerID.y != forwardID.y) neighbors.Add(new Vector2(0,1));
        if(centerID.x != backwardID.x) neighbors.Add(new Vector2(-1,0));
        if(centerID.y != backwardID.y) neighbors.Add(new Vector2(0,-1));
        if(neighbors.Count > 2) neighbors.Add(neighbors[1] + neighbors[2]);

        Vector2[] idsInRange = new Vector2[neighbors.Count];
        for(int i = 0; i < neighbors.Count; i++){
            idsInRange[i] = centerID + neighbors[i];
        }

        Debug.Log(centerID.ToString() + " " + forwardID.ToString() + " " + backwardID.ToString());
        Debug.Log(pos.ToString() + " " + neighbors.Count);
        return idsInRange;
    }

    public void RedrawChunksInRange(Vector2[] range){
        foreach(Vector2 id in range){
            if(chunkDictionary.ContainsKey(id)){
                DrawChunk[] chunks = chunkDictionary[id];
                foreach(DrawChunk c in chunks){
                    c.Draw();
                }
            }
        }
    }

    Vector2 GetIDFromPosition(Vector2 pos){
        return new Vector2(Mathf.FloorToInt(pos.x / (float) (chunkSize)), Mathf.FloorToInt(pos.y / (float) (chunkSize)));
    }
}

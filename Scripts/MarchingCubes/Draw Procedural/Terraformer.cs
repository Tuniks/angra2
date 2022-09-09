using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Terraformer : MonoBehaviour{
    [SerializeField] ChunkManager chunkManager;

    private Dictionary<Vector2, List<Vector3>> pointsByChunkID = new Dictionary<Vector2, List<Vector3>>();
    public float pointSize = 1f;

    public void AddDensityPoint(Vector3 pos){
        Vector2[] range = chunkManager.GetChunksInRange(pos, pointSize);
        
        foreach (Vector2 r in range){
            if(!pointsByChunkID.ContainsKey(r)) pointsByChunkID[r] = new List<Vector3>();
            pointsByChunkID[r].Add(pos);
        }

        chunkManager.RedrawChunksInRange(range);
    }

    public Vector3[] GetDensityPoints(Vector2 chunkID){
        if(!pointsByChunkID.ContainsKey(chunkID)) return new Vector3[0];
        
        return pointsByChunkID[chunkID].ToArray();
    }
}

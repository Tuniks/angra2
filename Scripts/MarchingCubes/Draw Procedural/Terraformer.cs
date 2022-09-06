using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Terraformer : MonoBehaviour{
    [SerializeField] ChunkManager chunkManager;

    private List<Vector3> densityPoints = new List<Vector3>();
    public float pointSize = 1f;

    public void AddDensityPoint(Vector3 pos){
        densityPoints.Add(pos);
        chunkManager.RedrawChunksInRange(pos, pointSize);
    }

    public Vector3[] GetDensityPoints(){
        return densityPoints.ToArray();
    }
}

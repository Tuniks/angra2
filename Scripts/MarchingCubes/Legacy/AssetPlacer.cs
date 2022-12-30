using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssetPlacer : MonoBehaviour {
    public GameObject asset;

    [Range(0, 5000)]
    public int maxInstancesNumber;
    public int maxInstancesPerChunk;
    public float rayHeight = 3000f;
    public float raycastDistance = 2000f;
    public float angleCutoff = 0f;
    public float noiseScale = 0.1f;
    [Range(0,1)] public float noiseCutoff = 0.5f;

    public int seed = 25;

    // Queue<GameObject> instances  = new Queue<GameObject>();
    Pool assetPool;

    void Start(){
        Random.InitState(TerrainData.seed);
        assetPool = new Pool(maxInstancesNumber, asset);
    }

    public void PlaceAssets(Vector3 startPos, float length){
        for(int i = 0; i < maxInstancesPerChunk; i++){
            Vector3 rayPosition = startPos;
            rayPosition.x += Random.Range(0f, length);
            rayPosition.y = rayHeight;
            rayPosition.z += Random.Range(0f, length);

            Vector2 noiseCoord = new Vector2(rayPosition.x, rayPosition.z) * noiseScale;
            if(Mathf.PerlinNoise(noiseCoord.x, noiseCoord.y) >= noiseCutoff){
                LaunchRay(rayPosition);
                Debug.DrawRay(rayPosition, Vector3.down * raycastDistance, Color.red, 10);
            } 
        }  
    }

    void LaunchRay(Vector3 position) {
        RaycastHit hit;

        if (Physics.Raycast(position, Vector3.down, out hit, raycastDistance)){
            if(Vector3.Dot(Vector3.up, hit.normal) < angleCutoff){
                return;
            }

            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            assetPool.Add(hit.point, spawnRotation, this.transform);
        }
    }


    public class Pool {
        Queue<GameObject> instances  = new Queue<GameObject>();
        int maxInstancesNumber;
        GameObject asset;

        public Pool(int max, GameObject prefab){
            maxInstancesNumber = max;
            asset = prefab;
        }

        public void Add(Vector3 position, Quaternion rotation, Transform parent){
            if(instances.Count >= maxInstancesNumber){
                GameObject old = instances.Dequeue();
                old.transform.position = position;
                old.transform.rotation = rotation;
                instances.Enqueue(old);
            } else {
                GameObject instance = Instantiate(asset, position, rotation);
                instance.transform.parent = parent;
                instances.Enqueue(instance);
            }
        }
    }
}

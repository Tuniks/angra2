using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

public struct ChunkToRender {
    public Chunk chunk;
    public Vector3 position;
    public int verticalChunk;
}

public class Chunk{
    Vector2 position;
    public Vector2 chunkID;
    public int lod;
    MapManager mapManager;
    
    int verticalChunks = 3;
    
    GameObject parentObject;
    GameObject[] chunkObjects;
    MeshRenderer[] meshRenderers;
    MeshFilter[] meshFilters;

    MeshCollider meshCollider;
    public bool shouldPlaceAssets = false; 
    AssetPlacer assetPlacer;

    public Chunk(Vector2 chunkID, int size, int lod, Transform parent, bool _shouldPlaceAssets, AssetPlacer _assetPlacer, MapManager _mapManager) {
        this.chunkID = chunkID;
        this.lod = lod;
        position = chunkID * size;
        shouldPlaceAssets = _shouldPlaceAssets;
        assetPlacer = _assetPlacer;
        mapManager = _mapManager;

        parentObject = new GameObject("Terrain Chunk " + chunkID.ToString());
        chunkObjects = new GameObject[verticalChunks];
        meshRenderers = new MeshRenderer[verticalChunks];
        meshFilters = new MeshFilter[verticalChunks];

        Vector3 position3d = new Vector3(position.x, 0, position.y);
        parentObject.transform.position = position3d * MapManager.terrainScale;
        parentObject.transform.parent = parent;
        parentObject.SetActive(false);

        for(int i = 0; i < verticalChunks; i++){
            chunkObjects[i] = new GameObject("chunk " + i.ToString());

            meshRenderers[i] = chunkObjects[i].AddComponent<MeshRenderer>();
            meshFilters[i] = chunkObjects[i].AddComponent<MeshFilter>();
            meshRenderers[i].material = mapManager.GetBiomeMaterial(position);

            position3d.y = i * size;
            chunkObjects[i].transform.position = position3d * MapManager.terrainScale;
            chunkObjects[i].transform.parent = parentObject.transform;
            chunkObjects[i].transform.localScale = Vector3.one * MapManager.terrainScale;
        }

        meshCollider = chunkObjects[1].AddComponent<MeshCollider>(); // Only collide w middle chunk
    }

    public void UpdateData(Vector2 chunkID, int size, int currentLOD, bool _shouldPlaceAssets) {
        parentObject.name = "Terrain Chunk " + chunkID.ToString();
        parentObject.SetActive(false);
        
        this.chunkID = chunkID;
        position = chunkID * size;
        Vector3 position3d = new Vector3(position.x, 0, position.y);
        parentObject.transform.position = position3d * MapManager.terrainScale;
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
        assetPlacer.PlaceAssets(new Vector3(position.x, 0, position.y) * MapManager.terrainScale, (MapManager.chunkSize - 1) * MapManager.terrainScale);
    }
}


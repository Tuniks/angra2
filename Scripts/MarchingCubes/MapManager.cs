using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using UnityEngine.Rendering;

public class MapManager : MonoBehaviour {
    public TerrainData terrainData;

    public BiomeDensityData[] biomeDensityData;
    public Material[] biomeMaterials;

    public ComputeShader VertexSharingMarchingCubeShader;
    public ComputeShader DensityNoiseTextureShader;

    public const int chunkSize = 103;
    // public const int chunkSize = 205;

    public const float chunkScale = 1f;
    public float surfaceLevel;
    public Vector2[] noiseParameters;
    public Vector2 previewOffset;

    public bool autoUpdate;

    public Vector4 function;

    // ================ VERTEX SHARING MARCHING CUBES ============
    public void GenerateMapDataTexture(Vector2 center, int lod, int chunkSize, RenderTexture mapData) {
        Vector3 center3d = new Vector3(center.x, 0, center.y);
        DensityGenerator densityGenerator = new DensityGenerator();
        densityGenerator.GenerateMapDensityTexture(mapData, chunkSize, chunkScale, lod, biomeDensityData, center3d, DensityNoiseTextureShader, function);
    }

    public RenderTexture CreateTextureBuffer(int meshSize){
        RenderTexture tex = new RenderTexture(meshSize+1, meshSize+1, 0, RenderTextureFormat.ARGBInt);
        tex.enableRandomWrite = true;
        tex.dimension = TextureDimension.Tex3D;
        tex.volumeDepth = meshSize+1;
        tex.wrapMode = TextureWrapMode.Clamp;

        tex.Create();

        return tex;
    }

    // Only used for mono LOD terrain generating
    public Mesh GenerateSharedVerticesMesh(RenderTexture mapDataTexture, int lod, int relativeChunkSize){
        SharedMCMesh meshGenerator = new SharedMCMesh();
        return meshGenerator.CreateMesh(mapDataTexture, relativeChunkSize, chunkScale, surfaceLevel, lod, VertexSharingMarchingCubeShader);
    }

    public Material GetBiomeMaterial(Vector2 chunkPosition){
        // Use non relative chunksize
        Vector2 biomeCoord;
        biomeCoord.x = Mathf.FloorToInt((chunkPosition.x+1) / (206 * 10f));
        biomeCoord.y = Mathf.FloorToInt((chunkPosition.y+1) / (206 * 10f));

        float centerValue = WhiteNoise.GetWhiteNoise(biomeCoord);
        float stepSize = 1f/biomeMaterials.Length;

        int biomeID = Mathf.FloorToInt(Mathf.Clamp(centerValue,0,1)/stepSize);

        return biomeMaterials[biomeID];
    }

    // === 3D CHUNK EXP ===
    public void GenerateMapDataTextureFrom3DOrigin(Vector3 center, int lod, int chunkSize, RenderTexture mapData) {
        DensityGenerator densityGenerator = new DensityGenerator();
        densityGenerator.GenerateMapDensityTexture(mapData, chunkSize, chunkScale, lod, biomeDensityData, center, DensityNoiseTextureShader, function);
    }
}

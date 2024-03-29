﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityGenerator {
    public ComputeShader DensityNoiseShader;
    
    ComputeBuffer pointsBuffer;
    ComputeBuffer parametersBuffer;
    ComputeBuffer offsetsBuffer;
    ComputeBuffer terraformingPoints;
    private int noThreads = 8;
    
    Vector4 function = TerrainData.function;
    private int kernelID = 0;

    const int seed = 26;

    private Dictionary<int, string> biomes = new Dictionary<int, string>(){
        {0, "IslandDensity"},
        {1, "DunesDensity"},
        {2, "MountainsDensity"},
        {3, "RockOceanDensity"}
    };

    void CreateBuffers(int octaves, int terraformerCount) {
        int octavesCount = octaves;
        if (octavesCount <= 0) octavesCount = 1;
        parametersBuffer = new ComputeBuffer(octavesCount, 2 * sizeof(float));
        offsetsBuffer = new ComputeBuffer(octavesCount, 3 * sizeof(float));

        if(terraformerCount == 0) terraformerCount++;
        terraformingPoints = new ComputeBuffer(terraformerCount, 3 * sizeof(float));
    }

    public void ReleaseBuffers() {
        if(offsetsBuffer != null || parametersBuffer != null ) {
            offsetsBuffer.Release();
            parametersBuffer.Release();
        }

        if(terraformingPoints != null) terraformingPoints.Release();
    }

    // ============== DENSITY ON TEXTURE ===================

    public void GenerateMapDensityTexture(RenderTexture pointsTexture, int gridSize, float gridScale, int lod, BiomeDensityData[] biomeData, Vector3 center, Terraformer terraformer, Vector2 chunkID, ComputeShader cs) {
        if(gridSize == 0) return;

        DensityNoiseShader = cs;

        Vector2[] noiseParameters = biomeData[kernelID].noiseParameters;
        int octaves = noiseParameters.Length; 
        Vector3[] terraformerData = terraformer.GetDensityPoints(chunkID);

        CreateBuffers(octaves, terraformerData.Length);

        Random.InitState(TerrainData.seed);
        Vector3[] octaveOffsets = new Vector3[octaves];
        for (int i = 0; i < octaves; i++) {
            octaveOffsets[i] = new Vector3 (Random.Range(-99999,99999), Random.Range(-99999,99999), Random.Range(-99999,99999));
        }

        //Set compute shader variables
        offsetsBuffer.SetData(octaveOffsets);
        parametersBuffer.SetData(noiseParameters);

        DensityNoiseShader.SetTexture(kernelID, "points", pointsTexture);
        DensityNoiseShader.SetBuffer(kernelID, "noiseOffsets", offsetsBuffer);
        DensityNoiseShader.SetBuffer(kernelID, "noiseParameters", parametersBuffer);
        DensityNoiseShader.SetInt("gridSize", gridSize);
        DensityNoiseShader.SetInt("octaves", octaves);
        DensityNoiseShader.SetInt("lod", lod);
        DensityNoiseShader.SetFloat("gridScale", gridScale);
        // Decrease one from the center to get a padded density texture to help w/ normal calculation
        DensityNoiseShader.SetVector("center", center - Vector3.one);
        DensityNoiseShader.SetVector("function", function);
        // Setting terraforming variables
        terraformingPoints.SetData(terraformerData);
        DensityNoiseShader.SetBuffer(kernelID, "terraformingPoints", terraformingPoints);
        DensityNoiseShader.SetFloat("sqrPointSize", terraformer.pointSize * terraformer.pointSize);
        DensityNoiseShader.SetInt("pointCount", terraformerData.Length);

        // Dispatching the baby
        // TODO Optimize thread and group size so you dont need a conditional on the shader
        // TODO resource https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/sv-dispatchthreadid
        int threadGroups = Mathf.CeilToInt(gridSize/ (float) noThreads);
        DensityNoiseShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ReleaseBuffers();
    }

    public int GetBiomeKernel(Vector2 center){
        // Use non relative chunksize
        Vector2 biomeCoord;
        Random.InitState(TerrainData.seed);

        biomeCoord.x = Mathf.FloorToInt((center.x) / (204 * 10f)) + Random.Range(-100,100);
        biomeCoord.y = Mathf.FloorToInt((center.y) / (204 * 10f)) + Random.Range(-100,100);

        float centerValue = WhiteNoise.GetWhiteNoise(biomeCoord);
        float stepSize = 1f/biomes.Count;

        int biomeID = Mathf.FloorToInt(Mathf.Clamp(centerValue,0,1)/stepSize);

        kernelID = biomeID;
        return biomeID;
    }
}

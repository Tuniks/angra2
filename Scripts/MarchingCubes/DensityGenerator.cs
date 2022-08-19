using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityGenerator {
    public ComputeShader DensityNoiseShader;
    
    ComputeBuffer pointsBuffer;
    ComputeBuffer parametersBuffer;
    ComputeBuffer offsetsBuffer;
    private int noThreads = 8;

    const int seed = 26;

    private Dictionary<int, string> biomes = new Dictionary<int, string>(){
        {0, "IslandDensity"},
        {1, "DunesDensity"},
        {2, "MountainsDensity"},
        {3, "RockOceanDensity"}
    };

    void CreateBuffers(int octaves) {
        int octavesCount = octaves;

        if (octavesCount <= 0) octavesCount = 1;

        parametersBuffer = new ComputeBuffer(octavesCount, 2 * sizeof(float));
        offsetsBuffer = new ComputeBuffer(octavesCount, 3 * sizeof(float));
    }

    public void ReleaseBuffers() {
        if(offsetsBuffer != null || parametersBuffer != null ) {
            offsetsBuffer.Release();
            parametersBuffer.Release();
        }
    }

    // ============== DENSITY ON TEXTURE ===================

    public void GenerateMapDensityTexture(RenderTexture pointsTexture, int gridSize, float gridScale, int lod, BiomeDensityData[] biomeData, Vector3 center, ComputeShader cs, Vector4 function) {
        if(gridSize == 0) return;

        DensityNoiseShader = cs;

        int kernelID = GetBiomeKernel(new Vector2(center.x, center.z));
        Vector2[] noiseParameters = biomeData[kernelID].noiseParameters;
        int octaves = noiseParameters.Length; 

        CreateBuffers(octaves);

        Random.InitState(1996);
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
        DensityNoiseShader.SetVector("center", center);
        DensityNoiseShader.SetVector("function", function);

        //Dispatching the baby
        // TODO Optimize thread and group size so you dont need a conditional on the shader
        // TODO resource https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/sv-dispatchthreadid
        int threadGroups = Mathf.CeilToInt(gridSize/ (float) noThreads);
        DensityNoiseShader.Dispatch(kernelID, threadGroups, threadGroups, threadGroups);

        ReleaseBuffers();
    }

    int GetBiomeKernel(Vector2 center){
        // Use non relative chunksize
        Vector2 biomeCoord;
        biomeCoord.x = Mathf.FloorToInt((center.x) / (204 * 10f));
        biomeCoord.y = Mathf.FloorToInt((center.y) / (204 * 10f));

        float centerValue = WhiteNoise.GetWhiteNoise(biomeCoord);
        float stepSize = 1f/biomes.Count;

        int biomeID = Mathf.FloorToInt(Mathf.Clamp(centerValue,0,1)/stepSize);

        return biomeID;
    }
}

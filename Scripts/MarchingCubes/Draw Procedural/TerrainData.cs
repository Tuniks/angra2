using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TerrainData{
    public const int chunkSize = 203;
    public const float scale = 1f;
    public const float surfaceLevel = 10f;

    public static Vector2[] noiseParameters = new Vector2[6] {
        new Vector2(0.09f, 1),
        new Vector2(0.042f, 3.94f),
        new Vector2(0.022f, 11.34f),
        new Vector2(0.009f, 21.4f),
        new Vector2(0.0047f, 68),
        new Vector2(0.0022f, 123)
    };

}

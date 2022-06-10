using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WhiteNoise {

    // Algorithm taken from https://www.youtube.com/watch?v=nohGiVNWhJE
    static public float GetWhiteNoise(Vector2 uv){
        float dot = Vector2.Dot(uv, new Vector2(12.9898f, 78.233f));
        dot = Mathf.Sin(dot);
        dot *= 43758.5453f;
        float frac = dot - Mathf.Floor(dot);

        return frac;
    }

}

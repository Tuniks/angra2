using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TerrainData : UpdatableData {
    public float meshHeightMultiplier;
    public AnimationCurve meshHeightCurve;

    public float scale = 5f;

    public float minHeight {
        get {
            return scale * meshHeightMultiplier * meshHeightCurve.Evaluate(0);
        }
    }

    public float maxHeight {
        get {
            return scale * meshHeightMultiplier * meshHeightCurve.Evaluate(1);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PostProcWater : MonoBehaviour {
    public GameObject volume;

    void Update() {
        volume.transform.position = new Vector3(this.transform.position.x, volume.transform.position.y, this.transform.position.z);
    }
}

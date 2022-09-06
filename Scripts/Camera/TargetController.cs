using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetController : MonoBehaviour{
    [SerializeField] private GameObject target;
    [SerializeField] private Terraformer terraformer;

    private float scrollSpeed = 1f;
    private float current = 0;
    private float maxDistance = 100f;

    void Update(){
        if(Input.GetKeyDown(KeyCode.T)){
            ToggleActive();
        }

        if(Input.mouseScrollDelta.y != 0){
            current = target.transform.localPosition.z + Input.mouseScrollDelta.y * scrollSpeed;
            target.transform.localPosition = new Vector3(0, 0, Mathf.Clamp(current, 10, maxDistance));
        }

        if(Input.GetMouseButtonDown(0)){
            DrawTerrain();
        }
    }

    private void ToggleActive(){
        target.SetActive(!target.activeSelf);
    }

    private void DrawTerrain(){
        terraformer.AddDensityPoint(target.transform.position);
    }

    
}

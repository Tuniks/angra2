using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine;

public class StartButton : MonoBehaviour{
    [SerializeField] TMP_InputField seedField;

    public void OnButtonClick(){
        string seed;
        if(seedField.text == ""){
            seed = Time.time.ToString();
        } else {
            seed = seedField.text;
        }

        Debug.Log(seed.GetHashCode());
        TerrainData.seed = seed.GetHashCode();
        SceneManager.LoadScene("MainScene");
    }

}

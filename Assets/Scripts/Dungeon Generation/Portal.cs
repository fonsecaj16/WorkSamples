using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    void OnTriggerEnter(Collider hitInfo)
    {
        Debug.Log("hubo portal");
        if (hitInfo.name == "Player" )
        {

            SwitchScene();
        }
    }
    void SwitchScene()
    {
        int c = SceneManager.sceneCount;
        
        RoomController.Instance.CurrentWorldName = "SegundoPiso";
        for (int i = 0; i < c; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            print(scene.name);

            SceneManager.UnloadSceneAsync(scene);


        }
        SceneManager.LoadScene("SegundoPisoMain");
    }
}

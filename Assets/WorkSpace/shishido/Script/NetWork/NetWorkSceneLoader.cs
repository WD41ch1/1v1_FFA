using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetWorkSceneLoader : MonoBehaviour
{
    [SerializeField]
    private string SCENE_NAME = "ConnectionLoadScene";

    public  void LoadScene()
    {
        //if (!nob.Owner.IsActive)
        //    return;

        SceneLoadData sld = new SceneLoadData(SCENE_NAME);
        sld.ReplaceScenes = ReplaceOption.None;
        InstanceFinder.SceneManager.LoadConnectionScenes(sld);
    }
}

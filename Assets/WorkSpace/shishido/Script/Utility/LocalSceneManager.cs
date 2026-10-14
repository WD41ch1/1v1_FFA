using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LocalSceneManager
{

    public static void LoadScene(string _sceneName)
    {
        if (_sceneName == string.Empty)
            return;

        SceneManager.LoadScene(_sceneName);
    }

}

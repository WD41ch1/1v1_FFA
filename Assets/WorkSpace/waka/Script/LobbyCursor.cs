using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public class LobbyCursor : MonoBehaviour
{
    private void OnEnable()
    {
        UpdateCursor();
    }

    private void LateUpdate()
    {
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // バトル中の制御はPlayerInputControllerに任せる。
        if (sceneName == "BattleScene" ||
            sceneName == "BattleScene_β")
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
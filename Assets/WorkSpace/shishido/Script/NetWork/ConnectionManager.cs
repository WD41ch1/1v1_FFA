using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private string SCENE_NAME = "ConnectionLoadScene";
    [SerializeField] private GameObject _canvas;
    [SerializeField] private Camera _connectCamera;

    public void StartHost()
    {
        StartServer();
        StartClient();
    }

    public void StartServer()
    {
        _networkManager.ServerManager.StartConnection();
    }

    public void StartClient()
    {
        _networkManager.ClientManager.StartConnection();
    }

    public void SetIPAddress(string text)
    {
        _networkManager.TransportManager.Transport.SetClientAddress(text);
    }

    public void LoadScene()
    {
#if true
        //if (!nob.Owner.IsActive)
        //    return;

        _canvas.SetActive(false);

        SceneLoadData sld = new SceneLoadData(SCENE_NAME);
        sld.ReplaceScenes = ReplaceOption.All;
        //InstanceFinder.SceneManager.LoadConnectionScenes(sld);
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
#else
        _connectCamera.gameObject.SetActive(false);
        _canvas.SetActive(false);

#endif
    }

}
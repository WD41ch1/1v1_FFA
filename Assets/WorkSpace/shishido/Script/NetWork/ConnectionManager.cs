using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private string SCENE_NAME = "LobbyScene";
    public bool IsConnected { get; private set; }

    private void Awake()
    {
        _networkManager.ClientManager.OnAuthenticated += OnAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ClientManager.OnAuthenticated -= OnAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void OnAuthenticated()
    {
        IsConnected = true;

        Debug.Log("Serverへの接続に成功しました");

        SceneLoadData loadData = new SceneLoadData(SCENE_NAME);

        loadData.ReplaceScenes = ReplaceOption.All;

        _networkManager.SceneManager.LoadGlobalScenes(loadData);

    }

    private void OnClientConnectionState(
        FishNet.Transporting.ClientConnectionStateArgs args)
    {
        Debug.Log($"Client状態: {args.ConnectionState}");
    }

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

    private void OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        Debug.Log(
            $"Player {connection.ClientId} が接続しました"
        );

        LoadLobby(connection);
    }


    private void LoadLobby(NetworkConnection connection)
    {
        SceneLoadData loadData =
            new SceneLoadData("Lobby");

        InstanceFinder.SceneManager.LoadConnectionScenes(
            connection,
            loadData
        );
    }
}
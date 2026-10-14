using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameConst;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private string _localHost = "127.0.0.1";
    public bool IsConnected { get; private set; }

    private void Awake()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        _networkManager.ClientManager.OnAuthenticated += OnAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

        if(_inputField != null)
            _inputField.text = _localHost;
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

        GameDebug.Client("Serverへの接続に成功しました");

        SceneLoadData loadData = new SceneLoadData(SCENE_LOBBY);

        loadData.ReplaceScenes = ReplaceOption.All;

        _networkManager.SceneManager.LoadGlobalScenes(loadData);

    }

    private void OnClientConnectionState(
        FishNet.Transporting.ClientConnectionStateArgs args)
    {
        GameDebug.Client($"Client状態: {args.ConnectionState}");
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
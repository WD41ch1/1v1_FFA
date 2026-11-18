using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using static GameConst;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Discovery;
using System;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private ConnectionMonitoringManager _monitoringManager;
    [SerializeField] private TMP_InputField _inputField;
    public bool IsConnected { get; private set; }


    private void Awake()
    {

        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        _networkManager.ClientManager.OnClientConnectionState
            += OnClientConnectionState;

    }

    private void Start()
    {
        if (_monitoringManager == null)
        {
            _monitoringManager = ConnectionMonitoringManager.Instance;
        }

        if (_inputField != null)
        {
            WaitGetMonitoringManager();
            _inputField.text = _monitoringManager._localHost;
        }
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.ClientManager.OnClientConnectionState
                -= OnClientConnectionState;
        }
    }

    private void OnClientConnectionState(
        FishNet.Transporting.ClientConnectionStateArgs args)
    {
        GameDebug.Client($"Client状態: {args.ConnectionState}");
    }

    public void StartHost()
    {
        //StartServer();
        //StartClient();

        _monitoringManager.CreateRoom();
    }

    public void StartClient()
    {
        _monitoringManager.StartClient();
    }

    public void SearchRoom()
    {
        _monitoringManager.SearchRoom();
    }

    public void SetIPAddress(string text)
    {
        _networkManager.TransportManager.Transport.SetClientAddress(text);
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

    private async void WaitGetMonitoringManager()
    {
        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        await UniTask.WaitUntil(() => _monitoringManager != null, cancellationToken: ct);
    }
}
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
    [SerializeField] private NetworkDiscovery _networkDiscovery;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private string _localHost = "127.0.0.1";
    [SerializeField] private int _derision = 10000;
    public bool IsConnected { get; private set; }

    private CancellationTokenSource _searchCts;

    private void Awake()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        _networkManager.ClientManager.OnAuthenticated += OnAuthenticated;
        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;

        // サーバー発見イベント
        _networkDiscovery.ServerFoundCallback += OnServerFound;

        if (_inputField != null)
            _inputField.text = _localHost;
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.ClientManager.OnAuthenticated -= OnAuthenticated;
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        if (_networkDiscovery != null)
        {
            _networkDiscovery.ServerFoundCallback -= OnServerFound;
            _networkDiscovery.StopSearchingOrAdvertising();
        }
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
        //StartServer();
        //StartClient();

        CreateRoom();
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

    #region 部屋作成

    public async void CreateRoom()
    {
        // サーバー起動
        _networkManager.TransportManager.Transport.SetPort(7777);

        StartServer();

        // サーバー起動完了まで待機
        await UniTask.WaitUntil(
            () => _networkManager.IsServerStarted
        );

        // LAN内に部屋を広告
        _networkDiscovery.AdvertiseServer();

        // ホスト自身もクライアントとして接続
        SetIPAddress(_localHost);
        StartClient();

    }
    #endregion

    #region 部屋検索

    public async void SearchRoom()
    {
        // すでに検索中なら何もしない
        if (_searchCts != null)
            return;

        _searchCts = new CancellationTokenSource();

        try
        {
            _networkDiscovery.SearchForServers();

            GameDebug.Client("部屋を検索しています...");

            // 5秒間検索
            await UniTask.Delay(
                _derision,
                cancellationToken: _searchCts.Token
            );

            // 5秒経過しても見つからなかった場合
            _networkDiscovery.StopSearchingOrAdvertising();

            GameDebug.Client("部屋が見つかりませんでした。");
        }
        catch (OperationCanceledException)
        {
            // 検索中に部屋が見つかった場合
        }
        finally
        {
            _searchCts?.Dispose();
            _searchCts = null;
        }
    }

    /// <summary>
    /// サーバーが見つかったときの処理
    /// </summary>
    /// <param name="endPoint"></param>
    private void OnServerFound(System.Net.IPEndPoint endPoint)
    {
        // 検索停止
        _networkDiscovery.StopSearchingOrAdvertising();

        string address = endPoint.Address.ToString();

        GameDebug.Client($"部屋を発見しました: {address}");

        // 発見したサーバーに接続
        SetIPAddress(address);

        _networkManager.TransportManager.Transport.SetPort(7777);

        StartClient();
    }

    #endregion
}
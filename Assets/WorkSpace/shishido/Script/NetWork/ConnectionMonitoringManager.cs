using Cysharp.Threading.Tasks;
using DG.Tweening;
using FishNet;
using FishNet.Connection;
using FishNet.Discovery;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using NaughtyAttributes;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static GameConst;

public enum DisConnectMenuState
{
    show,
    hide,
}


public class ConnectionMonitoringManager : MonoBehaviour
{
    public static ConnectionMonitoringManager Instance { get; private set; }

    #region private

    [SerializeField]
    private NetworkManager _networkManager;
    [SerializeField]
    private NetworkDiscovery _networkDiscovery;


    // 接続が成立したことがあるか
    private bool _wasConnected;

    // 自分で切断ボタンを押したか
    private bool _isManualDisconnect;

    // 接続Sceneへの遷移が開始済みか
    private bool _isReturning;

    private LocalConnectionState _clientConnectionState
= LocalConnectionState.Stopped;

    private RectTransform rect;
    private DisConnectMenuState d_state;
    private float toggleTime = 0.2f;
    private bool toggle;
    private CancellationToken ct;
    private CancellationTokenSource _searchCts;

    #endregion

    #region SerializeField

    [Header("接続情報")]
    [SerializeField] public string _localHost = "127.0.0.1";
    [SerializeField] private int _derision = 10000;

    [Header("GUI")]
    [Header("DisConnectGUI")]
    [SerializeField] private GameObject _ConnectionLostOBJ;
    [SerializeField] private GameObject _disConnectOBJ;
    [SerializeField] private Button _backButton;
    [SerializeField] private Button _ExitButton;
    [SerializeField] private RectTransform _ShowPos;
    [SerializeField] private RectTransform _HidePos;

    #endregion

    private void Awake()
    {
        ct = this.GetCancellationTokenOnDestroy();

        // 既に存在する場合は重複を防ぐ
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーン遷移後も保持
        DontDestroyOnLoad(gameObject);

        //  UIの初期化
        Init();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    private void OnEnable()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;

        if (_networkManager == null)
            return;

        if (_networkDiscovery == null)
        {
            _networkDiscovery = FindAnyObjectByType<NetworkDiscovery>();
        }

        _networkManager.ClientManager.OnClientConnectionState
            -= OnClientConnectionState;

        _networkManager.ClientManager.OnClientConnectionState
            += OnClientConnectionState;

        //// サーバー発見イベント
        //_networkDiscovery.ServerFoundCallback
        //    += OnServerFound;

        _networkManager.ClientManager.OnAuthenticated
            += OnAuthenticated;


        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackLoadScene);

        if (_backButton != null)
            _ExitButton.onClick.AddListener(OnExtiMatchi);
    }

    private void OnDisable()
    {
        if (_networkManager == null)
            return;

        _networkManager.ClientManager.OnClientConnectionState
            -= OnClientConnectionState;

        _networkManager.ClientManager.OnAuthenticated
            -= OnAuthenticated;


        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();

        if (_networkDiscovery != null)
        {
            _networkDiscovery.ServerFoundCallback -= OnServerFound;
            _networkDiscovery.StopSearchingOrAdvertising();
        }

    }

    #region 部屋作成
    public void StartServer()
    {
        _networkManager.ServerManager.StartConnection();
    }

    public void StartClient()
    {
        _networkManager.ClientManager.StartConnection();
    }


    public async void CreateRoom(string _port)
    {
        // 1. サーバー起動
        _networkManager.TransportManager.Transport.SetPort(7777);
        _networkManager.ServerManager.StartConnection();

        // IsServerStarted になるまでポーリングで待機
        await UniTask.WaitUntil(() => _networkManager.IsServerStarted, cancellationToken: ct);

        // 2. アドバタイズ開始（ゲストが LAN で探索できるようにする）
        _networkDiscovery.AdvertiseServer();

        // 3. ホスト自身もクライアントとして接続（リッスンサーバー）

        string port = "";
        if (_port == string.Empty)
            port = _localHost;
        else
            port = _port;

        _networkManager.TransportManager.Transport.SetClientAddress(port);
        StartClient();

        await UniTask.WaitUntil(() => _networkManager.IsClientStarted, cancellationToken: ct);

        // 4. 相手の入室を ServerManager のイベントで検知
        //_networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
    }

    void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            GameDebug.Client($"ゲストが入室しました: {conn.ClientId}");
            // ゲストが入室したら、ホストはアドバタイズを停止する
            _networkDiscovery.StopSearchingOrAdvertising();
            //  ゲストの接続を検知したら、ホストは Lobby シーンに遷移する
            OnAuthenticated();
        }
    }

    public void OnAuthenticated()
    {
        //IsConnected = true;

        GameDebug.Client("Serverへの接続に成功しました");

        SceneLoadData loadData = new SceneLoadData(SCENE_LOBBY);

        loadData.ReplaceScenes = ReplaceOption.All;

        _networkManager.SceneManager.LoadGlobalScenes(loadData);

    }


    #endregion

    #region 部屋検索

    public async void SearchRoom()
    {
        GameDebug.Client($"IsClientStarted: {_networkManager.IsClientStarted}");

        await FindAndJoinRoomAsync(ct);
    }

    /// <summary>
    /// サーバーが見つかったときの処理
    /// </summary>
    /// <param name="endPoint"></param>
    private void OnServerFound(System.Net.IPEndPoint endPoint)
    {
        GameDebug.Client("[Discovery] サーバーを発見しました！");
        GameDebug.Client($"IP: {endPoint.Address}");

        // 検索停止
        _networkDiscovery.StopSearchingOrAdvertising();

        string address = endPoint.Address.ToString();

        GameDebug.Client($"部屋を発見しました: {address}");

        // 発見したサーバーに接続
        _networkManager.TransportManager.Transport.SetClientAddress(address);
        _networkManager.TransportManager.Transport.SetPort(7777);

        StartClient();
    }

    async UniTask FindAndJoinRoomAsync(CancellationToken ct)
    {
        // 1. サーバー探索
        string serverAddress = null;
        _networkDiscovery.ServerFoundCallback += endPoint =>
        {
            if (serverAddress == null) serverAddress = endPoint.Address.ToString();
        };
        _networkDiscovery.SearchForServers();

        // 2. アドレスが取得できるまで待機
        await UniTask.WaitUntil(() => serverAddress != null, cancellationToken: ct);
        _networkDiscovery.StopSearchingOrAdvertising();

        // 3. 接続
        var transport = _networkManager.TransportManager.Transport;
        transport.SetClientAddress(serverAddress);
        transport.SetPort(7777);
        _networkManager.ClientManager.StartConnection();

        //await UniTask.WaitUntil(() => _networkManager.IsClientStarted, cancellationToken: ct);

        // 4. ホストの開始ブロードキャストを待つ
    }
    #endregion

    #region ConnectionMonitoring

    /// <summary>
    /// Clientの接続状態を監視
    /// </summary>
    private void OnClientConnectionState(
        ClientConnectionStateArgs args)
    {
        // FishNetから受け取った状態を保存
        _clientConnectionState = args.ConnectionState;

        GameDebug.Client(
            $"Client状態: {GetClientConnectionState()}");

        // 接続が成立したら
        if (IsConnectionSuccess(args))
            return;

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _wasConnected = true;
            return;
        }

        if (args.ConnectionState != LocalConnectionState.Stopped)
            return;

        // ここから切断後の処理
        if (!_wasConnected || _isManualDisconnect || _isReturning)
            return;

        _wasConnected = false;
        _isReturning = true;

        GameDebug.Warning("サーバーとの接続が切断されました");

        _ConnectionLostOBJ.SetActive(true);
    }

    /// <summary>
    /// 接続が成功したら
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    private bool IsConnectionSuccess(ClientConnectionStateArgs args)
    {
        // 接続が成立した
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _wasConnected = true;

            // 次の切断に備えてリセット
            _isReturning = false;
            _isManualDisconnect = false;

            GameDebug.Client("接続状態をリセットしました");
            return true;
        }

        return false;
    }

    #endregion

    #region ボタン登録用関数

    public void OnBackLoadScene()
    {
        LocalSceneManager.LoadScene(SCENE_CONNECTIONLOBBY);

        if (_ConnectionLostOBJ?.activeSelf == true)
            _ConnectionLostOBJ?.SetActive(false);
    }

    public void OnExtiMatchi()
    {
        OnDisConnection();

        Hide();
    }

    /// <summary>
    /// 切断ボタン
    /// </summary>
    public void OnDisConnection()
    {
        GameDebug.Log("[切断ボタン]または[マッチ退出ボタン]が押されました");

        if (_isReturning)
            return;

        _isManualDisconnect = true;
        _isReturning = true;

        bool isHost = _networkManager.IsHostStarted;

        if (isHost)
        {
            // Host：Clientへ切断通知を送り、部屋を閉じる
            if (InstanceFinder.ServerManager.Started)
                InstanceFinder.ServerManager.StopConnection(true);

            // Host自身のClientも停止
            if (InstanceFinder.ClientManager.Started)
                InstanceFinder.ClientManager.StopConnection();

            if (_networkDiscovery.IsAdvertising)
                _networkDiscovery.StopSearchingOrAdvertising();

            GameDebug.Server("Hostを終了しました");
        }
        else
        {
            // Client：ロビーから退出
            if (InstanceFinder.ClientManager.Started)
                InstanceFinder.ClientManager.StopConnection();

            _networkDiscovery.StopSearchingOrAdvertising();

            GameDebug.Client("ロビーから退出しました");
        }

        // 自分の画面はここで遷移
        LocalSceneManager.LoadScene(SCENE_CONNECTIONLOBBY);
    }

    #endregion

    #region UI関連

    private void Init()
    {
        //  ConnectionLostは隠しておく
        if (_ConnectionLostOBJ != null)
            _ConnectionLostOBJ.SetActive(false);


        if (_disConnectOBJ == null || _ShowPos == null || _HidePos == null)
        {
            _disConnectOBJ.SetActive(false);
        }
        else
        {
            rect = _disConnectOBJ.GetComponent<RectTransform>();
            Hide();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleDisConnectMenu();
        }
    }
    [Button]
    public void ToggleDisConnectMenu()
    {
        //  BattleScene出ない場合利用不可
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SCENE_BATTLE)
            return;

        toggle = !toggle;

        //  すでに同じ状態であるなら
        if (toggle && d_state == DisConnectMenuState.show)
            return;
        else if (!toggle && d_state == DisConnectMenuState.hide)
            return;

        //  表示切替処理
        if (toggle)
            Show();
        else
            Hide();

    }

    private void Show()
    {
        if (d_state == DisConnectMenuState.show)
            return;

        rect.DOAnchorPos(_ShowPos.anchoredPosition, toggleTime);
        d_state = DisConnectMenuState.show;
        CameraController.DisConnectMenuOpen = true;
    }

    private void Hide()
    {
        if (d_state == DisConnectMenuState.hide)
            return;

        rect.DOAnchorPos(_HidePos.anchoredPosition, toggleTime);
        d_state = DisConnectMenuState.hide;
        CameraController.DisConnectMenuOpen = false;
    }

    #endregion

    /// <summary>
    /// Clientの現在状態を取得
    /// </summary>
    public LocalConnectionState GetClientConnectionState()
    {
        return _clientConnectionState;
    }
}
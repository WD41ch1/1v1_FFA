using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using static GameConst;


public class ConnectionMonitoringManager : MonoBehaviour
{
    public static ConnectionMonitoringManager Instance { get; private set; }

    #region private

    [SerializeField]
    private NetworkManager _networkManager;

    // 接続が成立したことがあるか
    private bool _wasConnected;

    // 自分で切断ボタンを押したか
    private bool _isManualDisconnect;

    // 接続Sceneへの遷移が開始済みか
    private bool _isReturning;

    private LocalConnectionState _clientConnectionState
= LocalConnectionState.Stopped;

    #endregion

    #region SerializeField

    [SerializeField] private GameObject _disConnectCanvas;
    [SerializeField] private Button _backButton;

    #endregion

    private void Awake()
    {
        // 既に存在する場合は重複を防ぐ
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーン遷移後も保持
        DontDestroyOnLoad(gameObject);

        _disConnectCanvas?.SetActive(false);
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

        _networkManager.ClientManager.OnClientConnectionState
            -= OnClientConnectionState;

        _networkManager.ClientManager.OnClientConnectionState
            += OnClientConnectionState;

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackLoadScene);
    }

    private void OnDisable()
    {
        if (_networkManager == null)
            return;

        _networkManager.ClientManager.OnClientConnectionState
            -= OnClientConnectionState;

        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

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

        _disConnectCanvas.SetActive(true);
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

    #region ボタン登録用関数

    public void OnBackLoadScene()
    {
        LocalSceneManager.LoadScene(SCENE_CONNECTIONLOBBY);

        if (_disConnectCanvas?.activeSelf == true)
            _disConnectCanvas?.SetActive(false);
    }

    /// <summary>
    /// 切断ボタン
    /// </summary>
    public void OnDisConnection()
    {
        GameDebug.Log("切断ボタンが押されました");

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

            GameDebug.Server("Hostを終了しました");
        }
        else
        {
            // Client：ロビーから退出
            if (InstanceFinder.ClientManager.Started)
                InstanceFinder.ClientManager.StopConnection();

            GameDebug.Client("ロビーから退出しました");
        }

        // 自分の画面はここで遷移
        LocalSceneManager.LoadScene(SCENE_CONNECTIONLOBBY);
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
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameConst;

public class LobbyManager : NetworkBehaviour
{
    #region Const

    //  マッチ開始最低人数
    private const int MATCHI_MIN_PLAYERS = 1;

    #endregion

    #region Private
    private NetworkManager _networkManager;

    //  準備完了フラグ
    private bool _isReady = false;

    // 全員の準備が完了しているか
    private readonly SyncVar<bool> allParticipantsReady = new();

    private readonly SyncList<ParticipantData> participants
        = new SyncList<ParticipantData>();

    private readonly List<GameObject> participantObjects
        = new List<GameObject>();

    private List<NetworkConnection> matchPlayers
        = new List<NetworkConnection>();


    #endregion

    #region SerializeField
    [SerializeField] private Transform participantListPos;
    [SerializeField] private GameObject participantPrefab;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button disConectButton;
    [SerializeField] private int prevParticipants;
    #endregion

    #region Main
    private void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;

        participants.OnChange += OnParticipantsChanged;

        //  readyButtonの登録
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReady);
            ReadyButtonUpdate(_isReady);
        }

        //  disConectButtonの登録
        if (disConectButton != null &&
            ConnectionMonitoringManager.Instance != null)
        {
            ConnectionMonitoringManager cmm
                = ConnectionMonitoringManager.Instance;
            disConectButton.onClick.AddListener(cmm.OnDisConnection);
            disConectButtonUpdate(_networkManager.IsHostStarted);
        }
        else
        {
            GameDebug.Warning($"LobbyManager:" +
                $"[ConnectionMonitoringManager]" +
                $"もしくは[disConectButton]がNullです");
        }
    }

    private void Update()
    {
        if (Surveillance_participants())
        {
            RefreshParticipantList();
        }

        prevParticipants = participants.Count;
    }

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
        if (InstanceFinder.ClientManager != null)
        {
        }
    }

    private void OnDestroy()
    {
        //isReady.OnChange -= OnReadyChange;
        participants.OnChange -= OnParticipantsChanged;
    }

    #endregion

    #region override

    public override void OnStartServer()
    {
        base.OnStartServer();
        GameDebug.Server(
        $"LobbyManager OnStartServer / " +
        $"IsSpawned={IsSpawned}, " +
        $"IsServerInitialized={IsServerInitialized}"
        );

        // Host自身を登録
        if (InstanceFinder.ClientManager.Started)
        {
            InLobby(InstanceFinder.ClientManager.Connection);
        }

        //  リモートクライアントの接続状態変化を検知
        InstanceFinder.ServerManager.OnRemoteConnectionState +=
            OnRemoteConnectionState;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        InstanceFinder.ServerManager.OnRemoteConnectionState -=
            OnRemoteConnectionState;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        GameDebug.Client(
        $"LobbyManager OnStartClient / " +
        $"IsSpawned={IsSpawned}, " +
        $"IsClientInitialized={IsClientInitialized}"
        );

        allParticipantsReady.OnChange += OnAllReadyChanged;

        SetInteractive_ReadyButton(allParticipantsReady.Value);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        allParticipantsReady.OnChange -= OnAllReadyChanged;
    }

    #endregion

    #region Getter

    public SyncList<ParticipantData> GetParticipants()
    {
        return participants;
    }

    public ParticipantPlate GetParticipantPlate(int _id)
    {
        ParticipantPlate plate = null;

        foreach (GameObject obj in participantObjects)
        {
            ParticipantPlate pp = obj.GetComponent<ParticipantPlate>();

            if (_id == pp._playerID)
            {
                plate = pp;
                break;
            }
        }

        return plate;
    }

    /// <summary>
    /// 現在サーバーに接続しているクライアント数取得
    /// </summary>
    /// <returns></returns>
    public int GetConnectionCount()
    {
        return InstanceFinder.ServerManager.Clients.Count;
    }

    #endregion

    #region Setter

    #endregion

    #region UI

    /// <summary>
    /// disConectButtonの見た目変更
    /// </summary>
    /// <param name="_isHost"></param>
    private void disConectButtonUpdate(bool _isHost)
    {
        if (disConectButton != null)
        {
            Image image = disConectButton.GetComponent<Image>();
            TextMeshProUGUI TMP = disConectButton.GetComponentInChildren<TextMeshProUGUI>();

            //  Host
            if (_isHost)
            {
                TMP.text = "Close the Room";
            }
            //  Cliant
            else
            {
                TMP.text = "DisConnection";
            }
        }

    }


    /// <summary>
    /// 準備ボタン
    /// </summary>
    public void OnReady()
    {
        //isReady.Value = !isReady.Value;
        _isReady = !_isReady;

        //  ボタンの見た目変更
        ReadyButtonUpdate(_isReady);
        //  データ更新
        SetMatchStandby(_isReady);

    }

    public void OnReadyChange(
        bool previous,
        bool next,
        bool asServer
        )
    {
        //  ボタンの見た目変更
        ReadyButtonUpdate(next);
        //  データ更新
        SetMatchStandby(next);
    }

    private void SetInteractive_ReadyButton(bool allReady)
    {
        if (allReady)
            HideReadyButton();
        else
            ShowReadyButton();
    }

    /// <summary>
    /// 見た目変更
    /// </summary>
    /// <param name="flag"></param>
    private void ReadyButtonUpdate(bool flag)
    {
        if (readyButton != null)
        {
            Image image = readyButton.GetComponent<Image>();
            TextMeshProUGUI TMP = readyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (flag)
            {
                image.color = Color.red;
                TMP.text = "Ready:ON";
            }
            else
            {
                image.color = Color.green;
                TMP.text = "Ready:OFF";
            }
        }
    }

    private void HideReadyButton()
    {
        if (readyButton == null)
            return;

        readyButton.interactable = false;
    }

    private void ShowReadyButton()
    {
        if (readyButton == null)
            return;

        readyButton.interactable = true;
    }

    /// <summary>
    /// 参加者リストの生成
    /// </summary>
    private void RefreshParticipantList()
    {
        // 現在のUIを削除
        foreach (GameObject obj in participantObjects)
        {
            Destroy(obj);
        }

        participantObjects.Clear();

        // 現在の参加者を全部生成
        foreach (ParticipantData participant in participants)
        {
            GameObject obj =
                Instantiate(participantPrefab, participantListPos);

            ParticipantPlate plate =
                obj.GetComponent<ParticipantPlate>();

            bool isHost = participant.IsHost;

            bool you =
                participant.PlayerId ==
                InstanceFinder.ClientManager.Connection.ClientId;

            plate.Initialize(
                participant,
                you
            );

            participantObjects.Add(obj);
        }
    }

    /// <summary>
    /// participantsに変更が加わった時に呼び出されるメソッド
    /// </summary>
    /// <param name="op"></param>
    /// <param name="index"></param>
    /// <param name="oldItem"></param>
    /// <param name="newItem"></param>
    /// <param name="asServer"></param>
    private void OnParticipantsChanged(
        SyncListOperation op,
        int index,
        ParticipantData oldItem,
        ParticipantData newItem,
        bool asServer)
    {
        GameDebug.Log(
            $"OnParticipantsChanged: " +
            $"op={op}, asServer={asServer}, index={index}"
        );

        if (op != SyncListOperation.Set)
            return;

        //  前回の状態と今の状態を比較
        if (oldItem.IsReady != newItem.IsReady)
        {
            // 変更があった場合 

            GameDebug.Sync(
                $"{newItem.PlayerName}: " +
                $"{oldItem.IsReady} -> {newItem.IsReady}"
            );

            //  UIの更新
            ParticipantPlate plate = GetParticipantPlate(newItem.PlayerId);
            plate?.SetReady(newItem.IsReady);
        }
    }

    #endregion

    #region Lobby管理

    private bool Surveillance_participants()
    {
        if (prevParticipants != participants.Count)
            return true;

        return false;
    }

    /// <summary>
    /// リモートクライアントの接続状態変化を検知
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="args"></param>
    private void OnRemoteConnectionState(
    NetworkConnection connection,
    RemoteConnectionStateArgs args)
    {
        GameDebug.Server(
            $"接続状態: {args.ConnectionState}, " +
            $"接続数: {GetConnectionCount()}"
        );

        switch (args.ConnectionState)
        {
            case RemoteConnectionState.Started:
                InLobby(connection);
                break;

            case RemoteConnectionState.Stopped:
                RemoveParticipant(connection);
                break;
        }
    }

    /// <summary>
    /// Lobby参加処理
    /// </summary>
    /// <param name="connection"></param>
    private void InLobby(NetworkConnection connection)
    {
        ParticipantData data = new ParticipantData(
            $"Player {connection.ClientId}",
            connection.ClientId,
            connection.IsHost
        );

        participants.Add(data);

        GameDebug.Log($"AddPlayer : {data}");
    }


    /// <summary>
    /// 切断したプレイヤーのデータを SyncList から削除
    /// </summary>
    /// <param name="connection"></param>
    private void RemoveParticipant(NetworkConnection connection)
    {
        if (!IsServerInitialized)
            return;

        for (int i = participants.Count - 1; i >= 0; i--)
        {
            if (participants[i].PlayerId != connection.ClientId)
                continue;

            RemoveParticipantOBJ(connection.ClientId);
            participants.RemoveAt(i);

            GameDebug.Detail(
                $"参加者退出: {connection.ClientId}");

            break;
        }
    }

    /// <summary>
    /// 切断したプレイヤーを参加者リスト(UI)から削除
    /// </summary>
    /// <param name="_id"></param>
    private void RemoveParticipantOBJ(int _id)
    {
        for (int i = participantObjects.Count - 1; i >= 0; i--)
        {
            ParticipantPlate plate =
                participantObjects[i].GetComponent<ParticipantPlate>();

            if (plate?._playerID == _id)
            {
                plate.DisConnect();
                participantObjects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Ready状態の変更をサーバーに通知する
    /// </summary>
    /// <param name="standby"></param>
    private void SetMatchStandby(bool standby)
    {
        SetParticipantReadyServerRpc(standby);
    }

    /// <summary>
    /// ServerRpc：サーバー側で実行し、呼び出したプレイヤーのReady状態を更新する
    /// </summary>
    /// <param name="standby"></param>
    [ServerRpc(RequireOwnership = false)]
    private void SetParticipantReadyServerRpc(
    bool standby,
    NetworkConnection sender = null)
    {
        if (sender == null)
        {
            GameDebug.Warning("RPCの送信者を取得できませんでした");
            return;
        }

        int playerId = sender.ClientId;

        GameDebug.Server(
            $"Ready RPC受信: PlayerId={playerId}, Ready={standby}");

        SetParticipantReady(playerId, standby);

        // 更新後に全員のReady状態を判定
        CheckAllParticipantsReady();
    }


    /// <summary>
    /// SyncList内の指定プレイヤーを探し、Ready状態を更新する
    /// </summary>
    /// <param name="playerId"></param>
    /// <param name="ready"></param>
    [Server]
    public void SetParticipantReady(int playerId, bool ready)
    {
        GameDebug.Server(
            $"SetParticipantReady 呼び出し: " +
            $"PlayerId={playerId}, Ready={ready}"
        );

        for (int i = 0; i < participants.Count; i++)
        {
            ParticipantData data = participants[i];

            if (data.PlayerId != playerId)
                continue;

            GameDebug.Server(
                $"更新前: {data.PlayerName}, " +
                $"Ready={data.IsReady}"
            );

            data.SetReady(ready);
            participants[i] = data;

            GameDebug.Server(
                $"更新後: {data.PlayerName}, " +
                $"Ready={data.IsReady}"
            );

            return;
        }

        GameDebug.Warning(
            $"PlayerId={playerId} が participants に見つからない"
        );
    }

    #endregion

    #region Match開始管理

    [Server]
    private void CheckAllParticipantsReady()
    {
        bool allReady = IsAllParticipantsReady();

        // 判定結果を同期
        allParticipantsReady.Value = allReady;
    }

    /// <summary>
    /// 参加者全員の準備が完了しているか
    /// </summary>
    /// <returns></returns>
    [Server]
    private bool IsAllParticipantsReady()
    {
        int participantCount = participants.Count;
        int connectionCount = GetConnectionCount();

        GameDebug.Log(
            $"[AllReadyCheck] " +
            $"participants={participantCount}, " +
            $"connections={connectionCount}"
        );

        //  参加者データ数と参加人数が一致しているか
        if (participantCount != connectionCount)
        {
            GameDebug.Log("[AllReadyCheck] 人数が一致しない");
            return false;
        }

        //  最低参加人数に達しているか
        if (connectionCount < MATCHI_MIN_PLAYERS)
        {
            GameDebug.Log("[AllReadyCheck] 最低参加人数に達していない");
            return false;
        }

        foreach (ParticipantData data in participants)
        {
            GameDebug.Log(
                $"[AllReadyCheck] " +
                $"{data.PlayerName}: Ready={data.IsReady}"
            );

            if (!data.IsReady)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="prev"></param>
    /// <param name="next"></param>
    /// <param name="asServer"></param>
    private void OnAllReadyChanged(
    bool prev,
    bool next,
    bool asServer)
    {
        //  UIの更新
        SetInteractive_ReadyButton(next);

        //  シーンの遷移
        StartBattle();
    }

    [Server]
    public void StartBattle()
    {
        if (!IsServerInitialized)
            return;

        SceneLoadData loadData = new SceneLoadData(SCENE_BATTLE);

        loadData.ReplaceScenes = ReplaceOption.All;

        _networkManager.SceneManager.LoadGlobalScenes(loadData);
    }
    #endregion

}

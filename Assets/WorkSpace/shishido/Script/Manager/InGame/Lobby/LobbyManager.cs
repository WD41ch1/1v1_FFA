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
    #region Private
    private NetworkManager _networkManager;

    //  準備完了フラグ
    private bool _isReady = false;

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


    private void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;

        //isReady.OnChange += OnReadyChange;
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

        if (oldItem.IsReady != newItem.IsReady)
        {
            GameDebug.Sync(
                $"{newItem.PlayerName}: " +
                $"{oldItem.IsReady} -> {newItem.IsReady}"
            );

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

    private void OnRemoteConnectionState(
    NetworkConnection connection,
    RemoteConnectionStateArgs args)
    {
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



    public void StartBattle()
    {
        if (!IsServerInitialized)
            return;

        SceneLoadData sceneLoadData =
            new SceneLoadData("BattleScene");

        InstanceFinder.SceneManager.LoadConnectionScenes(
            sceneLoadData
        );
    }

    public void StartMatch(
        NetworkConnection playerA,
        NetworkConnection playerB)
    {
        if (!IsServerInitialized)
            return;

        matchPlayers.Clear();

        matchPlayers.Add(playerA);
        matchPlayers.Add(playerB);

        SceneLoadData loadData =
            new SceneLoadData("Battle");

        //InstanceFinder.SceneManager.LoadConnectionScenes(
        //    matchPlayers,
        //    loadData
        //);
    }


    #endregion

}

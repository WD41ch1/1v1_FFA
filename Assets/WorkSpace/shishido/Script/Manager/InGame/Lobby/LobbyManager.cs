using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LobbyManager : NetworkBehaviour
{
    private NetworkManager _networkManager;

    [SerializeField] private Transform participantListPos;
    [SerializeField] private GameObject participantPrefab;

    private readonly SyncList<ParticipantData> participants
        = new SyncList<ParticipantData>();

    private readonly List<GameObject> participantObjects
        = new List<GameObject>();

    [SerializeField]
    private int prevParticipants;

#if true

    private void Awake()
    {
        _networkManager = InstanceFinder.NetworkManager;
    }

    private void Update()
    {
        if (Surveillance_participants())
        {
            RefreshParticipantList();
        }

        prevParticipants = participants.Count;
    }


    private bool Surveillance_participants()
    {
        if (prevParticipants != participants.Count)
            return true;

        return false;
    }


    public override void OnStartServer()
    {
        base.OnStartServer();

        Debug.LogWarning("!!!");

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

    private void OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState != RemoteConnectionState.Started)
            return;

        InLobby(connection);
    }

    private void InLobby(NetworkConnection connection)
    {
        ParticipantData data = new ParticipantData(
            connection.ClientId,
            connection.IsHost,
            $"Player {connection.ClientId}"
        );

        participants.Add(data);

        Debug.Log($"AddPlayer : {data}");
    }

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
                participant.PlayerName,
                participant.PlayerId,
                isHost,
                you
            );

            participantObjects.Add(obj);
        }
    }
#else //    接続テスト
    public override void OnStartServer()
    {
        base.OnStartServer();

        Debug.Log("LobbyManager : OnStartServer");

        InstanceFinder.ServerManager.OnRemoteConnectionState +=
            OnRemoteConnectionState;
    }

    public override void OnStopServer()
    {
        base.OnStopServer();

        if (InstanceFinder.ServerManager != null)
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState -=
                OnRemoteConnectionState;
        }
    }

    private void OnRemoteConnectionState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        Debug.Log(
            $"Connection : {connection.ClientId} / " +
            $"State : {args.ConnectionState}"
        );
    }
    
#endif

}

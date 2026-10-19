using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using FishNet.Transporting;

// PlayerルートのNetworkObjectと同じ場所に追加する。
public class NetworkPlayerTeam : NetworkBehaviour
{
    private sealed class Session
    {
        public readonly string id =
            Guid.NewGuid().ToString("N");

        public int team = -1;
    }

    // 同じ接続でPlayerを再生成した場合は、
    // 同じ識別番号を使用する。
    private static ConditionalWeakTable<NetworkConnection, Session>
        sessions =
            new ConditionalWeakTable<NetworkConnection, Session>();

    private readonly SyncVar<string> playerId =
        new SyncVar<string>();

    private readonly SyncVar<int> teamId =
        new SyncVar<int>(-1);

    public string PlayerId => playerId.Value;

    // -1：チームなし
    // 0以上：チーム番号
    public int TeamId => teamId.Value;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessions()
    {
        sessions =
            new ConditionalWeakTable<NetworkConnection, Session>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        ServerManager.OnRemoteConnectionState -= OnRemoteState;
        ServerManager.OnRemoteConnectionState += OnRemoteState;

        if (Owner == null || !Owner.IsActive)
            return;

        Session session =
            sessions.GetValue(Owner, _ => new Session());

        playerId.Value = session.id;
        teamId.Value = session.team;
    }

    private static void OnRemoteState(
        NetworkConnection connection,
        RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            sessions.Remove(connection);
        }
    }

    /// <summary>
    /// サーバー側のチーム振り分け処理から呼ぶ。
    /// PlayerのOnStartServer完了後に使用する。
    /// </summary>
    public void SetTeamServer(int value)
    {
        if (!IsServerInitialized ||
            Owner == null ||
            !Owner.IsActive)
        {
            Debug.LogWarning(
                "チーム設定はPlayerのサーバー初期化後に行ってください。",
                this);
            return;
        }

        value = Mathf.Max(-1, value);

        sessions.GetValue(
            Owner,
            _ => new Session()).team = value;

        teamId.Value = value;
    }
}
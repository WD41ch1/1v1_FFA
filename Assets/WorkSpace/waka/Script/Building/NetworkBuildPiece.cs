using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class NetworkBuildPiece : NetworkBehaviour
{
    [Header("本物の地面のLayer")]
    public LayerMask groundLayer;

    [Header("編集権限の診断")]
    public bool logEditPermission = true;

    private readonly SyncVar<string> builderId =
        new SyncVar<string>();

    private readonly SyncVar<int> buildingTeamId =
        new SyncVar<int>(-1);

    public string BuilderId => builderId.Value;
    public int BuildingTeamId => buildingTeamId.Value;

    private BuildingSystem.BuildType kind;

    private static readonly HashSet<NetworkBuildPiece> serverPieces =
        new HashSet<NetworkBuildPiece>();

    // 同じ診断ログを毎フレーム出さないための記録。
    private readonly Dictionary<int, string> lastPermissionLogs =
        new Dictionary<int, string>();

    /// <summary>
    /// サーバー側で、Instantiate後・Spawn前に呼ぶ。
    /// 建築を要求したPlayerのNetworkPlayerTeamを渡す。
    /// </summary>
    public void Configure(
        BuildingSystem.BuildType type,
        LayerMask ground,
        NetworkPlayerTeam builder = null)
    {
        kind = type;
        groundLayer = ground;

        builderId.Value = "";
        buildingTeamId.Value = -1;

        if (builder == null)
        {
            Debug.LogError(
                "[建築者登録エラー] NetworkPlayerTeamが渡されていません。" +
                "NetworkBuildControllerのConfigure呼び出しを確認してください。",
                this);
            return;
        }

        if (!builder.IsServerInitialized ||
            !builder.IsSpawned ||
            builder.Owner == null ||
            !builder.Owner.IsActive)
        {
            Debug.LogError(
                "[建築者登録エラー] 建築者のPlayerが" +
                "サーバーで初期化されていません。",
                builder);
            return;
        }

        if (string.IsNullOrEmpty(builder.PlayerId))
        {
            Debug.LogError(
                "[建築者登録エラー] PlayerIdが空です。" +
                "NetworkPlayerTeamの初期化を確認してください。",
                builder);
            return;
        }

        builderId.Value = builder.PlayerId;
        buildingTeamId.Value = builder.TeamId;

        if (logEditPermission)
        {
            Debug.Log(
                $"[建築者登録] 建築={name}\n" +
                $"建築者Player={builder.name}\n" +
                $"建築者ID={BuilderId}\n" +
                $"建築チーム={BuildingTeamId}",
                this);
        }
    }

    /// <summary>
    /// 建てた本人、または同じチームだけ編集可能。
    /// ホストであることは許可条件に含めない。
    /// </summary>
    public bool CanEdit(NetworkPlayerTeam player)
    {
        bool allowed = EvaluatePermission(player, out string reason);

        if (logEditPermission)
        {
            LogPermissionOnce(player, allowed, reason);
        }

        return allowed;
    }

    private bool EvaluatePermission(
        NetworkPlayerTeam player,
        out string reason)
    {
        if (!IsSpawned)
        {
            reason = "建築がまだSpawnされていません。";
            return false;
        }

        if (player == null)
        {
            reason = "操作するPlayerにNetworkPlayerTeamがありません。";
            return false;
        }

        if (!player.IsSpawned)
        {
            reason = "PlayerがまだSpawnされていません。";
            return false;
        }

        if (string.IsNullOrEmpty(BuilderId))
        {
            reason = "建築者IDが未登録、またはまだ受信されていません。";
            return false;
        }

        if (string.IsNullOrEmpty(player.PlayerId))
        {
            reason = "PlayerIdが未登録、またはまだ受信されていません。";
            return false;
        }

        bool allowed = BuildingEditPermission.Allows(
            BuilderId,
            BuildingTeamId,
            player.PlayerId,
            player.TeamId);

        if (BuilderId == player.PlayerId)
        {
            reason = "建築した本人です。";
        }
        else if (allowed)
        {
            reason = "建築と同じチームです。";
        }
        else
        {
            reason = "建築した本人でも、同じチームでもありません。";
        }

        return allowed;
    }

    private void LogPermissionOnce(
        NetworkPlayerTeam player,
        bool allowed,
        string reason)
    {
        int key = player != null ? player.GetInstanceID() : 0;

        string playerName = player != null
            ? player.name
            : "なし";

        string playerIdText = player != null
            ? player.PlayerId
            : "";

        int playerTeam = player != null
            ? player.TeamId
            : -1;

        bool localOwner =
            player != null && player.IsOwner;

        string log =
            $"[編集権限] 建築={name}\n" +
            $"建築者ID={BuilderId}\n" +
            $"建築チーム={BuildingTeamId}\n" +
            $"確認対象Player={playerName}\n" +
            $"PlayerID={playerIdText}\n" +
            $"Playerチーム={playerTeam}\n" +
            $"この端末の操作対象={localOwner}\n" +
            $"編集可能={allowed}\n" +
            $"理由={reason}";

        if (lastPermissionLogs.TryGetValue(
                key,
                out string previous) &&
            previous == log)
        {
            return;
        }

        lastPermissionLogs[key] = log;
        Debug.Log(log, this);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        serverPieces.Add(this);

        BuildSupport.Attach(
            gameObject,
            groundLayer,
            null);
    }

    public override void OnStopServer()
    {
        serverPieces.Remove(this);

        BuildSupport support =
            GetComponent<BuildSupport>();

        if (support != null)
        {
            BuildSupportManager.Unregister(support);
        }

        lastPermissionLogs.Clear();

        base.OnStopServer();
    }

    public static bool Occupied(
        BuildingSystem.BuildType type,
        Vector3 position,
        Quaternion rotation)
    {
        serverPieces.RemoveWhere(
            piece => piece == null || !piece.IsSpawned);

        foreach (NetworkBuildPiece piece in serverPieces)
        {
            if (piece.kind != type)
                continue;

            if ((piece.transform.position - position).sqrMagnitude >
                0.0001f)
            {
                continue;
            }

            if (type == BuildingSystem.BuildType.Floor ||
                type == BuildingSystem.BuildType.Cone)
            {
                return true;
            }

            float angle = Mathf.Abs(
                Mathf.DeltaAngle(
                    piece.transform.eulerAngles.y,
                    rotation.eulerAngles.y));

            if (angle < 1f)
                return true;

            if (type == BuildingSystem.BuildType.Wall &&
                Mathf.Abs(angle - 180f) < 1f)
            {
                return true;
            }
        }

        return false;
    }

    public void DestroyOnServer()
    {
        if (!IsServerInitialized || !IsSpawned)
            return;

        Despawn(gameObject, DespawnType.Destroy);
    }

    [ContextMenu("サーバーで削除（崩壊テスト）")]
    private void TestDestroy()
    {
        if (Application.isPlaying)
        {
            DestroyOnServer();
        }
    }

    [ContextMenu("編集権限を確認")]
    private void DiagnoseEditPermission()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "再生中の、生成された建築で実行してください。",
                this);
            return;
        }

        lastPermissionLogs.Clear();

        Debug.Log(
            $"[建築の所有情報] 建築={name}\n" +
            $"建築者ID={BuilderId}\n" +
            $"建築チーム={BuildingTeamId}\n" +
            $"サーバー初期化済み={IsServerInitialized}\n" +
            $"クライアント初期化済み={IsClientInitialized}",
            this);

        NetworkPlayerTeam[] players =
            FindObjectsOfType<NetworkPlayerTeam>();

        foreach (NetworkPlayerTeam player in players)
        {
            bool allowed =
                EvaluatePermission(player, out string reason);

            LogPermissionOnce(player, allowed, reason);
        }
    }
}

public static class BuildingEditPermission
{
    public static bool Allows(
        string builder,
        int buildingTeam,
        string player,
        int playerTeam)
    {
        if (string.IsNullOrEmpty(builder) ||
            string.IsNullOrEmpty(player))
        {
            return false;
        }

        // 建築した本人。
        if (builder == player)
            return true;

        // チームなし同士は同じチームと扱わない。
        return buildingTeam >= 0 &&
               playerTeam >= 0 &&
               buildingTeam == playerTeam;
    }
}
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

    // プレビュー側でも種類・占有マスを確認できるよう同期する
    private readonly SyncVar<int> buildingKind =
        new SyncVar<int>(-1);

    private readonly SyncVar<Vector3> occupiedCell =
        new SyncVar<Vector3>();

    private readonly SyncVar<bool> hasOccupiedCell =
        new SyncVar<bool>(false);

    public string BuilderId => builderId.Value;
    public int BuildingTeamId => buildingTeamId.Value;

    private static readonly HashSet<NetworkBuildPiece> serverPieces =
        new HashSet<NetworkBuildPiece>();

    private static readonly HashSet<NetworkBuildPiece> clientPieces =
        new HashSet<NetworkBuildPiece>();

    private readonly Dictionary<int, string> lastPermissionLogs =
        new Dictionary<int, string>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistries()
    {
        serverPieces.Clear();
        clientPieces.Clear();
    }

    /// <summary>
    /// サーバーでInstantiate後、Spawn前に呼ぶ。
    /// pivotFromBottomは屋根の底面からPivotまでの高さ。
    /// </summary>
    public void Configure(
        BuildingSystem.BuildType type,
        LayerMask ground,
        NetworkPlayerTeam builder = null,
        float gridSize = 0f,
        float pivotFromBottom = 0f)
    {
        buildingKind.Value = (int)type;
        groundLayer = ground;

        hasOccupiedCell.Value =
            IsRampOrCone(type) && gridSize > 0f;

        if (hasOccupiedCell.Value)
        {
            occupiedCell.Value = GetCellPosition(
                transform.position,
                gridSize,
                pivotFromBottom);
        }

        builderId.Value = "";
        buildingTeamId.Value = -1;

        if (builder == null)
        {
            Debug.LogError(
                "[建築者登録エラー] NetworkPlayerTeamが渡されていません。",
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
                "[建築者登録エラー] PlayerIdが空です。",
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

    private static bool IsRampOrCone(BuildingSystem.BuildType type)
    {
        return type == BuildingSystem.BuildType.Ramp ||
               type == BuildingSystem.BuildType.Cone;
    }

    private static Vector3 GetCellPosition(
        Vector3 position,
        float gridSize,
        float pivotFromBottom)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            Mathf.Round(
                (position.y - pivotFromBottom) / gridSize) * gridSize,
            Mathf.Round(position.z / gridSize) * gridSize);
    }

    /// <summary>
    /// 階段・屋根は、同じマス・同じ階層を共有できない。
    /// 壁・床については従来の重複判定を使用する。
    /// </summary>
    public static bool Occupied(
        BuildingSystem.BuildType type,
        Vector3 position,
        Quaternion rotation,
        float gridSize = 0f,
        float pivotFromBottom = 0f,
        bool serverOnly = true)
    {
        HashSet<NetworkBuildPiece> pieces =
            serverOnly ? serverPieces : clientPieces;

        pieces.RemoveWhere(
            piece => piece == null || !piece.IsSpawned);

        bool checkCell = IsRampOrCone(type) && gridSize > 0f;

        Vector3 candidateCell = checkCell
            ? GetCellPosition(position, gridSize, pivotFromBottom)
            : Vector3.zero;

        foreach (NetworkBuildPiece piece in pieces)
        {
            BuildingSystem.BuildType existingType =
                (BuildingSystem.BuildType)piece.buildingKind.Value;

            // 階段と屋根の相互重複。
            // 回転やColliderの形状に関係なく判定する。
            if (checkCell &&
                IsRampOrCone(existingType) &&
                piece.hasOccupiedCell.Value)
            {
                if ((piece.occupiedCell.Value - candidateCell)
                    .sqrMagnitude < 0.0001f)
                {
                    return true;
                }
            }

            // 従来の同種建築の重複判定
            if (existingType != type)
                continue;

            if ((piece.transform.position - position)
                .sqrMagnitude > 0.0001f)
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

    public override void OnStartServer()
    {
        base.OnStartServer();

        serverPieces.Add(this);
        BuildSupport.Attach(gameObject, groundLayer, null);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        clientPieces.Add(this);
    }

    public override void OnStopClient()
    {
        clientPieces.Remove(this);
        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        serverPieces.Remove(this);

        BuildSupport support = GetComponent<BuildSupport>();

        if (support != null)
            BuildSupportManager.Unregister(support);

        lastPermissionLogs.Clear();

        base.OnStopServer();
    }

    public bool CanEdit(NetworkPlayerTeam player)
    {
        bool allowed = EvaluatePermission(player, out string reason);

        if (logEditPermission)
            LogPermissionOnce(player, allowed, reason);

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
            reason = "建築した本人です。";
        else if (allowed)
            reason = "建築と同じチームです。";
        else
            reason = "建築した本人でも、同じチームでもありません。";

        return allowed;
    }

    private void LogPermissionOnce(
        NetworkPlayerTeam player,
        bool allowed,
        string reason)
    {
        int key = player != null ? player.GetInstanceID() : 0;

        string log =
            $"[編集権限] 建築={name}\n" +
            $"建築者ID={BuilderId}\n" +
            $"建築チーム={BuildingTeamId}\n" +
            $"確認対象Player={(player != null ? player.name : "なし")}\n" +
            $"PlayerID={(player != null ? player.PlayerId : "")}\n" +
            $"Playerチーム={(player != null ? player.TeamId : -1)}\n" +
            $"この端末の操作対象={(player != null && player.IsOwner)}\n" +
            $"編集可能={allowed}\n" +
            $"理由={reason}";

        if (lastPermissionLogs.TryGetValue(key, out string previous) &&
            previous == log)
        {
            return;
        }

        lastPermissionLogs[key] = log;
        Debug.Log(log, this);
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
            DestroyOnServer();
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

        foreach (NetworkPlayerTeam player in
                 FindObjectsOfType<NetworkPlayerTeam>())
        {
            bool allowed = EvaluatePermission(player, out string reason);
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

        if (builder == player)
            return true;

        return buildingTeam >= 0 &&
               playerTeam >= 0 &&
               buildingTeam == playerTeam;
    }
}
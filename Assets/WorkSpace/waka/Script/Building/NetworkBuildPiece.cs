using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

// 完成品プレハブのルートに付ける。
public class NetworkBuildPiece : NetworkBehaviour
{
    public LayerMask groundLayer;

    private readonly SyncVar<string> builderId =
        new SyncVar<string>();

    private readonly SyncVar<int> buildingTeamId =
        new SyncVar<int>(-1);

    public string BuilderId => builderId.Value;
    public int BuildingTeamId => buildingTeamId.Value;

    private BuildingSystem.BuildType kind;

    private static readonly HashSet<NetworkBuildPiece> serverPieces =
        new HashSet<NetworkBuildPiece>();

    /// <summary>
    /// サーバー側でSpawnする前に呼ぶ。
    /// </summary>
    public void Configure(
        BuildingSystem.BuildType type,
        LayerMask ground,
        NetworkPlayerTeam builder = null)
    {
        kind = type;
        groundLayer = ground;

        bool validBuilder =
            builder != null &&
            builder.IsServerInitialized;

        builderId.Value =
            validBuilder ? builder.PlayerId : "";

        buildingTeamId.Value =
            validBuilder ? builder.TeamId : -1;
    }

    /// <summary>
    /// 自分、または同じチームの建築なら編集可能。
    /// </summary>
    public bool CanEdit(NetworkPlayerTeam player)
    {
        if (player == null)
            return false;

        return BuildingEditPermission.Allows(
            builderId.Value,
            buildingTeamId.Value,
            player.PlayerId,
            player.TeamId);
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
            BuildSupportManager.Unregister(support);

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
            DestroyOnServer();
    }
}

// このクラスも同じNetworkBuildPiece.cs内に書く。
public static class BuildingEditPermission
{
    public static bool Allows(
        string builder,
        int buildingTeam,
        string player,
        int playerTeam)
    {
        // 情報が未登録・未受信なら許可しない。
        if (string.IsNullOrEmpty(builder) ||
            string.IsNullOrEmpty(player))
        {
            return false;
        }

        // 建てた本人。
        if (builder == player)
            return true;

        // チーム未設定の-1同士は味方と扱わない。
        return buildingTeam >= 0 &&
               playerTeam >= 0 &&
               buildingTeam == playerTeam;
    }
}
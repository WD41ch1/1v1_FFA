using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using static GameConst;

public class NetworkBuildController : NetworkBehaviour
{
    [Header("建築スクリプト")]
    [SerializeField] private BuildWall wall;
    [SerializeField] private BuildRamp ramp;
    [SerializeField] private BuildFloor floor;
    [SerializeField] private BuildCone cone;

    [Header("建築制限")]
    [Min(1f)]
    public float maxBuildDistance = 12f;

    [Min(0.01f)]
    public float minRequestInterval = 0.1f;

    private InventoryManager inventory;

    private bool pending;
    private uint nextRequestId;
    private uint pendingId;

    private uint lastServerRequestId;
    private bool hasServerRequest;
    private float nextServerTime;

    private BildingMatType reservedType;
    private int reservedAmount;

    private void Awake()
    {
        if (wall == null)
            wall = GetComponentInChildren<BuildWall>(true);

        if (ramp == null)
            ramp = GetComponentInChildren<BuildRamp>(true);

        if (floor == null)
            floor = GetComponentInChildren<BuildFloor>(true);

        if (cone == null)
            cone = GetComponentInChildren<BuildCone>(true);

        inventory = GetComponent<InventoryManager>();
    }

    public void RequestBuild(
        BuildingSystem.BuildType type,
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsOwner || !IsClientInitialized || pending)
            return;

        if (!TryGetDefinition(
                type,
                out GameObject prefab,
                out LayerMask ground,
                out BildingMatType material,
                out int cost))
        {
            return;
        }

        if (inventory == null ||
            cost <= 0 ||
            inventory.GetMat(material) < cost)
        {
            return;
        }

        if (!inventory.TryConsumeBildMat(
                material,
                cost,
                out int consumed))
        {
            return;
        }

        if (consumed != cost)
        {
            if (consumed > 0)
                inventory.AddBildMat(material, consumed);

            return;
        }

        pending = true;
        pendingId = ++nextRequestId;

        reservedType = material;
        reservedAmount = consumed;

        RequestBuildServerRpc(
            pendingId,
            type,
            position,
            rotation.eulerAngles.y);
    }

    [ServerRpc]
    private void RequestBuildServerRpc(
        uint requestId,
        BuildingSystem.BuildType type,
        Vector3 position,
        float yaw)
    {
        if (hasServerRequest &&
            requestId <= lastServerRequestId)
        {
            return;
        }

        hasServerRequest = true;
        lastServerRequestId = requestId;

        if (Time.time < nextServerTime)
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "建築間隔が短すぎます。");
            return;
        }

        nextServerTime =
            Time.time + Mathf.Max(0.01f, minRequestInterval);

        if (!TryGetDefinition(
                type,
                out GameObject prefab,
                out LayerMask ground,
                out BildingMatType material,
                out int cost) ||
            !Finite(position.x) ||
            !Finite(position.y) ||
            !Finite(position.z) ||
            !Finite(yaw) ||
            Vector3.Distance(transform.position, position) >
            maxBuildDistance)
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "Prefab設定または建築距離を確認してください。");
            return;
        }

        NetworkObject prefabNetworkObject =
            prefab.GetComponent<NetworkObject>();

        if (prefabNetworkObject == null ||
            prefab.GetComponent<NetworkBuildPiece>() == null ||
            ground.value == 0)
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "完成PrefabにNetworkObject・NetworkBuildPieceと" +
                "Ground Layerの設定が必要です。");
            return;
        }

        // 屋根はBuildConeの角度を使用する。
        float placementYaw =
            type == BuildingSystem.BuildType.Cone
                ? cone.coneRotationY
                : Mathf.Round(yaw / 90f) * 90f;

        if (!Finite(placementYaw))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "回転設定が不正です。");
            return;
        }

        Quaternion rotation =
            Quaternion.Euler(0f, placementYaw, 0f);

        Physics.SyncTransforms();

        if (NetworkBuildPiece.Occupied(
                type,
                position,
                rotation) ||
            !CanPlace(type, position, rotation))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "サーバー側の設置条件を満たしません。");
            return;
        }

        NetworkPlayerTeam builder =
            GetComponent<NetworkPlayerTeam>();

        if (builder == null ||
            !builder.IsServerInitialized ||
            string.IsNullOrEmpty(builder.PlayerId))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "PlayerルートのNetworkPlayerTeamと" +
                "初期化状態を確認してください。");
            return;
        }

        GameObject placed =
            Instantiate(prefab, position, rotation);

        NetworkBuildPiece piece =
            placed.GetComponent<NetworkBuildPiece>();

        // 建築者と建築時点のチームを登録する。
        piece.Configure(type, ground, builder);

        // 建築自体のネットワーク所有者は付けない。
        Spawn(placed);

        Physics.SyncTransforms();

        if (!placed.GetComponent<NetworkObject>().IsSpawned)
        {
            Destroy(placed);

            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "Spawn失敗。Prefab登録を確認してください。");
            return;
        }

        BuildResultTargetRpc(
            Owner,
            requestId,
            true,
            "");
    }

    [TargetRpc]
    private void BuildResultTargetRpc(
        NetworkConnection connection,
        uint requestId,
        bool accepted,
        string reason)
    {
        if (!pending || pendingId != requestId)
            return;

        pending = false;

        if (!accepted)
        {
            if (inventory != null && reservedAmount > 0)
            {
                inventory.AddBildMat(
                    reservedType,
                    reservedAmount);
            }

            Debug.LogWarning(
                "建築できません：" + reason,
                this);
        }

        reservedAmount = 0;
    }

    public override void OnStopClient()
    {
        // 接続断時はサーバーの成否が不明なので自動返金しない。
        pending = false;
        reservedAmount = 0;

        base.OnStopClient();
    }

    private bool Finite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private bool CanPlace(
        BuildingSystem.BuildType type,
        Vector3 position,
        Quaternion rotation)
    {
        switch (type)
        {
            case BuildingSystem.BuildType.Wall:
                return wall != null &&
                       wall.ServerCanPlace(
                           position,
                           rotation,
                           transform);

            case BuildingSystem.BuildType.Ramp:
                return ramp != null &&
                       ramp.ServerCanPlace(
                           position,
                           rotation,
                           transform);

            case BuildingSystem.BuildType.Floor:
                return floor != null &&
                       floor.ServerCanPlace(
                           position,
                           rotation,
                           transform);

            case BuildingSystem.BuildType.Cone:
                return cone != null &&
                       cone.ServerCanPlace(
                           position,
                           rotation,
                           transform);

            default:
                return false;
        }
    }

    private bool TryGetDefinition(
        BuildingSystem.BuildType type,
        out GameObject prefab,
        out LayerMask ground,
        out BildingMatType material,
        out int cost)
    {
        prefab = null;
        ground = 0;
        material = default(BildingMatType);
        cost = 0;

        switch (type)
        {
            case BuildingSystem.BuildType.Wall:
                if (wall == null)
                    return false;

                prefab = wall.wallPrefab;
                ground = wall.groundLayer;
                material = wall.materialType;
                cost = wall.materialCost;
                break;

            case BuildingSystem.BuildType.Ramp:
                if (ramp == null)
                    return false;

                prefab = ramp.rampPrefab;
                ground = ramp.groundLayer;
                material = ramp.materialType;
                cost = ramp.materialCost;
                break;

            case BuildingSystem.BuildType.Floor:
                if (floor == null)
                    return false;

                prefab = floor.floorPrefab;
                ground = floor.groundLayer;
                material = floor.materialType;
                cost = floor.materialCost;
                break;

            case BuildingSystem.BuildType.Cone:
                if (cone == null)
                    return false;

                prefab = cone.conePrefab;
                ground = cone.groundLayer;
                material = cone.materialType;
                cost = cone.materialCost;
                break;

            default:
                return false;
        }

        return prefab != null;
    }
}
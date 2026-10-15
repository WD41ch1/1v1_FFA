using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using static GameConst;

// PlayerのNetworkObjectと同じGameObjectに付ける。
// 建材はローカル対戦テスト用に所有者側で予約。サーバー権威の所持数ではない。
public class NetworkBuildController : NetworkBehaviour
{
    [SerializeField] private BuildWall wall;
    [SerializeField] private BuildRamp ramp;
    [SerializeField] private BuildFloor floor;
    [SerializeField] private BuildCone cone;
    [Min(1f)] public float maxBuildDistance = 12f;
    [Min(0.01f)] public float minRequestInterval = 0.1f;

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
        if (wall == null) wall = GetComponentInChildren<BuildWall>(true);
        if (ramp == null) ramp = GetComponentInChildren<BuildRamp>(true);
        if (floor == null) floor = GetComponentInChildren<BuildFloor>(true);
        if (cone == null) cone = GetComponentInChildren<BuildCone>(true);
        inventory = GetComponent<InventoryManager>();
    }

    public void RequestBuild(
        BuildingSystem.BuildType type, Vector3 position, Quaternion rotation)
    {
        if (!IsOwner || !IsClientInitialized || pending) return;
        GameObject prefab;
        LayerMask ground;
        BildingMatType material;
        int cost;
        if (!TryGetDefinition(type, out prefab, out ground, out material, out cost))
            return;

        if (inventory == null || cost <= 0 || inventory.GetMat(material) < cost)
            return;

        int consumed;
        if (!inventory.TryConsumeBildMat(material, cost, out consumed))
            return;
        if (consumed != cost)
        {
            if (consumed > 0) inventory.AddBildMat(material, consumed);
            return;
        }

        pending = true;
        pendingId = ++nextRequestId;
        reservedType = material;
        reservedAmount = consumed;

        RequestBuildServerRpc(pendingId, type, position, rotation.eulerAngles.y);
    }

    [ServerRpc]
    private void RequestBuildServerRpc(
        uint requestId, BuildingSystem.BuildType type, Vector3 position, float yaw)
    {
        // 同じ要求の二重処理を防ぐ。
        if (hasServerRequest && requestId <= lastServerRequestId) return;
        hasServerRequest = true;
        lastServerRequestId = requestId;

        if (Time.time < nextServerTime)
        {
            BuildResultTargetRpc(Owner, requestId, false, "建築間隔が短すぎます。");
            return;
        }
        nextServerTime = Time.time + Mathf.Max(0.01f, minRequestInterval);

        GameObject prefab;
        LayerMask ground;
        BildingMatType material;
        int cost;
        if (!TryGetDefinition(type, out prefab, out ground, out material, out cost) ||
            !Finite(position.x) || !Finite(position.y) ||
            !Finite(position.z) || !Finite(yaw) ||
            Vector3.Distance(transform.position, position) > maxBuildDistance)
        {
            BuildResultTargetRpc(Owner, requestId, false, "Prefab設定または建築距離を確認してください。");
            return;
        }

        NetworkObject prefabNetworkObject = prefab.GetComponent<NetworkObject>();
        if (prefabNetworkObject == null ||
            prefab.GetComponent<NetworkBuildPiece>() == null ||
            ground.value == 0)
        {
            BuildResultTargetRpc(Owner, requestId, false,
                "完成PrefabにNetworkObject・NetworkBuildPieceとGround Layerの設定が必要です。");
            return;
        }

        float snappedYaw = Mathf.Round(yaw / 90f) * 90f;
        Quaternion rotation = Quaternion.Euler(0f, snappedYaw, 0f);
        Physics.SyncTransforms();

        if (NetworkBuildPiece.Occupied(type, position, rotation) ||
            !CanPlace(type, position, rotation))
        {
            BuildResultTargetRpc(Owner, requestId, false, "サーバー側の設置条件を満たしません。");
            return;
        }

        GameObject placed = Instantiate(prefab, position, rotation);
        NetworkBuildPiece piece = placed.GetComponent<NetworkBuildPiece>();
        piece.Configure(type, ground);

        // 所有者なし。建築したPlayerが退出しても建築は残す。
        Spawn(placed);
        Physics.SyncTransforms();

        if (!placed.GetComponent<NetworkObject>().IsSpawned)
        {
            Destroy(placed);
            BuildResultTargetRpc(Owner, requestId, false, "Spawn失敗。Prefab登録を確認してください。");
            return;
        }

        BuildResultTargetRpc(Owner, requestId, true, "");
    }

    [TargetRpc]
    private void BuildResultTargetRpc(
        NetworkConnection connection, uint requestId, bool accepted, string reason)
    {
        if (!pending || pendingId != requestId) return;

        pending = false;
        if (!accepted)
        {
            if (inventory != null && reservedAmount > 0)
                inventory.AddBildMat(reservedType, reservedAmount);
            Debug.LogWarning("建築できません：" + reason, this);
        }
        reservedAmount = 0;
    }

    public override void OnStopClient()
    {
        // 接続断時はサーバー側の成否が不明なので自動返金しない。
        pending = false;
        reservedAmount = 0;
        base.OnStopClient();
    }

    private bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool CanPlace(BuildingSystem.BuildType type, Vector3 p, Quaternion r)
    {
        switch (type)
        {
         // case BuildingSystem.BuildType.Wall: return wall != null && wall.ServerCanPlace(p, r, transform);
         // case BuildingSystem.BuildType.Ramp: return ramp != null && ramp.ServerCanPlace(p, r, transform);
         // case BuildingSystem.BuildType.Floor: return floor != null && floor.ServerCanPlace(p, r, transform);
         // case BuildingSystem.BuildType.Cone: return cone != null && cone.ServerCanPlace(p, r, transform);
            default: return false;
        }
    }

    private bool TryGetDefinition(
        BuildingSystem.BuildType type, out GameObject prefab,
        out LayerMask ground, out BildingMatType material, out int cost)
    {
        prefab = null;
        ground = 0;
        material = default(BildingMatType);
        cost = 0;
        switch (type)
        {
            case BuildingSystem.BuildType.Wall:
                if (wall == null) return false;
                prefab = wall.wallPrefab; ground = wall.groundLayer;
                material = wall.materialType; cost = wall.materialCost;
                break;
            case BuildingSystem.BuildType.Ramp:
                if (ramp == null) return false;
                prefab = ramp.rampPrefab; ground = ramp.groundLayer;
                material = ramp.materialType; cost = ramp.materialCost;
                break;
            case BuildingSystem.BuildType.Floor:
                if (floor == null) return false;
                prefab = floor.floorPrefab; ground = floor.groundLayer;
                material = floor.materialType; cost = floor.materialCost;
                break;
            case BuildingSystem.BuildType.Cone:
                if (cone == null) return false;
                prefab = cone.conePrefab; ground = cone.groundLayer;
                material = cone.materialType; cost = cone.materialCost;
                break;
            default: return false;
        }
        return prefab != null;
    }
}

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using static GameConst;

// PlayerのNetworkObject・InventoryManagerと
// 同じGameObjectに付ける。
public class NetworkBuildController : NetworkBehaviour
{
    [Header("Player内の建築スクリプト")]
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

    /// <summary>
    /// 所有者の操作から建築を要求する。
    /// 建材はローカル対戦テスト用に手元で予約消費する。
    /// </summary>
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
            Debug.LogWarning(
                $"建築設定を確認してください。種類={type}",
                this);
            return;
        }

        if (inventory == null)
        {
            Debug.LogWarning(
                "NetworkBuildControllerと同じオブジェクトに" +
                "InventoryManagerが必要です。",
                this);
            return;
        }

        if (cost <= 0 || inventory.GetMat(material) < cost)
            return;

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

    /// <summary>
    /// サーバーで設置を確認し、完成品を生成する。
    /// </summary>
    [ServerRpc]
    private void RequestBuildServerRpc(
        uint requestId,
        BuildingSystem.BuildType type,
        Vector3 position,
        float yaw)
    {
        // 同じ要求を二重に処理しない。
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
                out int cost))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                $"建築スクリプトまたは完成Prefabが未設定です。種類={type}");
            return;
        }

        if (!Finite(position.x) ||
            !Finite(position.y) ||
            !Finite(position.z) ||
            !Finite(yaw))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "設置位置または回転の値が不正です。");
            return;
        }

        if (Vector3.Distance(transform.position, position) >
            maxBuildDistance)
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "建築可能な距離を超えています。");
            return;
        }

        NetworkObject prefabNetworkObject =
            prefab.GetComponent<NetworkObject>();

        NetworkBuildPiece prefabBuildPiece =
            prefab.GetComponent<NetworkBuildPiece>();

        if (prefabNetworkObject == null ||
            prefabBuildPiece == null ||
            ground.value == 0)
        {
            string reason =
                $"[建築設定診断] 種類={type}, Prefab={prefab.name}\n" +
                $"NetworkObjectあり={prefabNetworkObject != null}\n" +
                $"NetworkBuildPieceあり={prefabBuildPiece != null}\n" +
                $"BuilderのGround Layer値={ground.value}\n" +
                $"処理したPlayer={gameObject.name}";

            Debug.LogWarning(reason, prefab);

            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                reason);
            return;
        }

        // 屋根の45度などの回転を、90度刻みに丸めない。
        // サーバー側のBuildCone設定を使う。
        float placementYaw;

        if (type == BuildingSystem.BuildType.Cone)
        {
            placementYaw = cone.coneRotationY;
        }
        else
        {
            placementYaw = Mathf.Round(yaw / 90f) * 90f;
        }

        if (!Finite(placementYaw))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "建築の回転設定が不正です。");
            return;
        }

        Quaternion rotation =
            Quaternion.Euler(0f, placementYaw, 0f);

        Physics.SyncTransforms();

        if (NetworkBuildPiece.Occupied(
                type,
                position,
                rotation))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "同じ位置・向きに建築が登録されています。");
            return;
        }

        if (!CanPlace(type, position, rotation))
        {
            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "設置判定で拒否されました。" +
                "ホスト側Consoleの[建築診断]を確認してください。");
            return;
        }

        GameObject placed =
            Instantiate(prefab, position, rotation);

        NetworkBuildPiece piece =
            placed.GetComponent<NetworkBuildPiece>();

        piece.Configure(type, ground);

        // 所有者なしで生成する。
        // 建築したPlayerが退出しても建築を残す。
        Spawn(placed);

        Physics.SyncTransforms();

        NetworkObject placedNetworkObject =
            placed.GetComponent<NetworkObject>();

        if (!placedNetworkObject.IsSpawned)
        {
            Destroy(placed);

            BuildResultTargetRpc(
                Owner,
                requestId,
                false,
                "Spawn失敗。完成Prefabの登録を確認してください。");
            return;
        }

        BuildResultTargetRpc(
            Owner,
            requestId,
            true,
            "");
    }

    /// <summary>
    /// 建築結果を要求したプレイヤーへ通知する。
    /// 拒否された場合は予約消費した建材を返す。
    /// </summary>
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
        // 接続断時はサーバー側の成否が不明なので、
        // 自動返金しない。
        pending = false;
        reservedAmount = 0;

        base.OnStopClient();
    }

    private bool Finite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    /// <summary>
    /// 各建築スクリプトの設置判定を呼ぶ。
    /// </summary>
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

    /// <summary>
    /// 各建築スクリプトから完成Prefab・地面Layer・
    /// 建材の種類と必要数を取得する。
    /// </summary>
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
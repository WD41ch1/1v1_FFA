using System.Collections.Generic;
using UnityEngine;
using static GameConst;

/// <summary>
/// 屋根建築を担当するクラス
///
/// ・床と同じグリッドへ配置
/// ・下向き：Playerの足元
/// ・正面：カメラ方向の隣接マス
/// ・上向き：Playerと同じマスの1段上
/// ・地面または既存建築につながる場合だけ候補を表示
/// ・床の上に重ねて配置可能
/// ・既存の屋根、階段との占有判定を維持
/// ・生成と建材消費はNetworkBuildControllerへ要求
/// </summary>
public class BuildCone : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Tooltip("Player本体")]
    public Transform player;

    [Header("建材消費")]
    [Tooltip("未設定ならPlayer参照の親から取得")]
    public PlayerManager playerManager;

    public BildingMatType materialType;

    [Min(1)]
    public int materialCost = 10;

    [Header("Prefab")]
    public GameObject conePrefab;
    public GameObject conePreviewPrefab;

    [Header("プレビューMaterial")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("Layer")]
    public LayerMask groundLayer;

    [Tooltip("完成した壁・階段・床・屋根など、すべての建築Layer")]
    public LayerMask buildLayer;

    [Tooltip("完成した床だけのLayer")]
    public LayerMask floorLayer;

    [Tooltip("完成した屋根だけのLayer")]
    public LayerMask coneLayer;

    [Header("グリッド設定")]
    [Tooltip("BuildFloorと同じ値にする")]
    public float gridSize = 4f;

    public float buildDistance = 6f;

    [Header("Cone設定")]
    [Tooltip("屋根の底面からPrefabのPivotまでの高さ")]
    public float conePivotFromBottom = 0f;

    [Tooltip("屋根全体の高さ")]
    public float coneHeight = 2f;

    [Tooltip("Prefabの向きを補正するY回転")]
    public float coneRotationY = 45f;

    [Header("視線設定")]
    [Range(-1f, 0f)]
    public float lookDownThreshold = -0.45f;

    [Tooltip("この値より上を向くと、現在のマスの1段上を候補にする")]
    [Range(0f, 1f)]
    public float lookUpThreshold = 0.3f;

    [Header("接続判定")]
    public float connectionThickness = 0.2f;

    private GameObject currentPreview;
    private PlayerMovement playerMovement;

    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    // 同じPlayerが使用する生成済みカメラを取得する。
    private bool TryResolvePlayerCamera()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();

            if (playerMovement == null && player != null)
            {
                playerMovement =
                    player.GetComponentInParent<PlayerMovement>();
            }
        }

        if (playerMovement == null ||
            playerMovement.cameraTransform == null)
        {
            return false;
        }

        Transform cameraTransform = playerMovement.cameraTransform;

        Camera generatedCamera =
            cameraTransform.GetComponent<Camera>();

        if (generatedCamera == null)
        {
            generatedCamera =
                cameraTransform.GetComponentInChildren<Camera>(true);
        }

        if (generatedCamera == null)
            return false;

        playerCamera = generatedCamera;
        return true;
    }

    public void ShowPreview()
    {
        HidePreview();

        if (conePreviewPrefab == null)
        {
            Debug.LogError(
                "BuildCone: Cone Preview Prefabが未設定です",
                this);
            return;
        }

        currentPreview = Instantiate(conePreviewPrefab);

        foreach (Collider col in
                 currentPreview.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        currentPreview.SetActive(true);
    }

    public void HidePreview()
    {
        if (currentPreview == null)
            return;

        Destroy(currentPreview);
        currentPreview = null;
    }

    public void UpdatePreview()
    {
        if (currentPreview == null)
            return;

        bool hasCandidate = TryGetBuildPoint(
            out Vector3 position,
            out Quaternion rotation,
            out bool canBuild);

        if (!hasCandidate)
        {
            currentPreview.SetActive(false);
            return;
        }

        currentPreview.SetActive(true);

        currentPreview.transform.SetPositionAndRotation(
            position,
            rotation);

        SetPreviewMaterial(canBuild && HasEnoughMaterials());
    }

    public void Build()
    {
        Physics.SyncTransforms();

        if (!TryGetBuildPoint(
                out Vector3 position,
                out Quaternion rotation,
                out bool canBuild) ||
            !canBuild)
        {
            return;
        }

        NetworkBuildController network =
            GetComponentInParent<NetworkBuildController>();

        if (network == null && player != null)
        {
            network =
                player.GetComponentInParent<NetworkBuildController>();
        }

        if (network == null)
        {
            Debug.LogError(
                "PlayerにNetworkBuildControllerを追加してください。",
                this);
            return;
        }

        network.RequestBuild(
            BuildingSystem.BuildType.Cone,
            position,
            rotation);
    }

    // サーバーで送られた設置位置を検証する。
    public bool ServerCanPlace(
        Vector3 position,
        Quaternion rotation,
        Transform playerRoot)
    {
        player = playerRoot;

        if (player == null)
            return false;

        bool connected = IsConnected(position);
        bool overlapping = IsOverlappingCone(position);
        bool playerOverlap = IsOverlappingPlayer(position);

        bool behind =
            !IsSamePlayerGrid(position) && IsBehindPlayer(position);

        bool allowed =
            connected &&
            !overlapping &&
            !playerOverlap &&
            !behind;

        if (!allowed)
        {
            Debug.LogWarning(
                $"[建築診断・屋根] " +
                $"接続あり={connected}, " +
                $"屋根との重複={overlapping}, " +
                $"プレイヤーとの重なり={playerOverlap}, " +
                $"プレイヤーの後方={behind}\n" +
                $"設置位置={position}, " +
                $"Player位置={player.position}, " +
                $"Player前方={player.forward}",
                this);
        }

        return allowed;
    }

    private bool TryGetMaterialInventory(
        out InventoryManager inventory)
    {
        inventory = null;

        if (playerManager == null && player != null)
        {
            playerManager =
                player.GetComponentInParent<PlayerManager>();
        }

        if (playerManager == null)
            return false;

        inventory = playerManager.inventoryManager;
        return inventory != null;
    }

    private bool HasEnoughMaterials()
    {
        return materialCost > 0 &&
               TryGetMaterialInventory(out InventoryManager inventory) &&
               inventory.GetMat(materialType) >= materialCost;
    }

    // 元の消費処理を保持。
    // Build()ではNetworkBuildControllerへ要求するため、ここでは呼ばない。
    private bool TryPayMaterials()
    {
        if (materialCost <= 0 ||
            !TryGetMaterialInventory(out InventoryManager inventory))
        {
            Debug.LogWarning(
                "BuildCone: Player Manager、Inventory Manager、" +
                "Material Costを確認してください。",
                this);
            return false;
        }

        if (inventory.GetMat(materialType) < materialCost)
            return false;

        return playerManager.TryConsumeBildMat(
                   materialType,
                   materialCost,
                   out int consumed) &&
               consumed == materialCost;
    }

    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild)
    {
        position = Vector3.zero;

        rotation = Quaternion.Euler(
            0f,
            coneRotationY,
            0f);

        canBuild = false;

        if (player == null ||
            gridSize <= 0f ||
            !TryResolvePlayerCamera())
        {
            return false;
        }

        Vector3 forward = GetSnappedPlayerForward();

        if (forward.sqrMagnitude <= 0.001f)
            return false;

        Vector3 playerGrid = new Vector3(
            Mathf.Round(player.position.x / gridSize) * gridSize,
            GetPlayerBuildLevel(),
            Mathf.Round(player.position.z / gridSize) * gridSize);

        float lookY = playerCamera.transform.forward.y;

        bool lookingDown = lookY < lookDownThreshold;
        bool lookingUp = lookY > lookUpThreshold;

        Vector3 buildPoint;

        // 修正：上向きなら隣のマスではなく、自分のマスの真上。
        if (lookingUp)
        {
            buildPoint = playerGrid + Vector3.up * gridSize;
        }
        else if (lookingDown)
        {
            buildPoint = playerGrid;
        }
        else
        {
            buildPoint = playerGrid + forward * gridSize;
        }

        position = GetConeGridPosition(buildPoint);

        // 接続がなければ候補を表示しない。
        if (!IsConnected(position))
            return false;

        canBuild = true;

        // 同じXZマスなら、真下・真上とも後方判定をしない。
        if (!IsSamePlayerGrid(position) && IsBehindPlayer(position))
            canBuild = false;

        if (builtPositions.Contains(GetBuildKey(position)))
            canBuild = false;

        if (IsOverlappingCone(position))
            canBuild = false;

        if (IsOverlappingPlayer(position))
            canBuild = false;

        return true;
    }

    private Vector3 GetConeGridPosition(Vector3 buildPoint)
    {
        float gridX =
            Mathf.Round(buildPoint.x / gridSize) * gridSize;

        float gridZ =
            Mathf.Round(buildPoint.z / gridSize) * gridSize;

        float buildLevel =
            Mathf.Round(buildPoint.y / gridSize) * gridSize;

        return new Vector3(
            gridX,
            buildLevel + conePivotFromBottom,
            gridZ);
    }

    private float GetPlayerBuildLevel()
    {
        float playerBottomY = player.position.y;

        Collider playerCollider = player.GetComponent<Collider>();

        if (playerCollider != null)
            playerBottomY = playerCollider.bounds.min.y;

        return Mathf.Round(playerBottomY / gridSize) * gridSize;
    }

    private Vector3 GetSnappedPlayerForward()
    {
        // サーバーなど、カメラ参照がない場合はPlayerの向きを使う。
        Transform directionSource =
            playerCamera != null ? playerCamera.transform : player;

        if (directionSource == null)
            return Vector3.forward;

        Vector3 forward = directionSource.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Quaternion.Euler(
                0f,
                directionSource.eulerAngles.y,
                0f) * Vector3.forward;
        }

        forward.Normalize();

        if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z))
            return new Vector3(Mathf.Sign(forward.x), 0f, 0f);

        return new Vector3(0f, 0f, Mathf.Sign(forward.z));
    }

    private bool IsConnected(Vector3 conePosition)
    {
        float coneBaseY =
            conePosition.y - conePivotFromBottom;

        Vector3 checkCenter = new Vector3(
            conePosition.x,
            coneBaseY - connectionThickness,
            conePosition.z);

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            connectionThickness,
            gridSize / 2f - 0.15f);

        int connectionMask =
            groundLayer.value |
            buildLayer.value |
            floorLayer.value |
            coneLayer.value;

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            connectionMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit) || IsPlayerCollider(hit))
                continue;

            return true;
        }

        return IsConnectedBeside(conePosition);
    }

    private bool IsConnectedBeside(Vector3 conePosition)
    {
        float coneBaseY =
            conePosition.y - conePivotFromBottom;

        float halfGrid = gridSize / 2f;

        Vector3 basePosition = new Vector3(
            conePosition.x,
            coneBaseY,
            conePosition.z);

        Vector3[] centers =
        {
            basePosition + Vector3.forward * halfGrid,
            basePosition + Vector3.back * halfGrid,
            basePosition + Vector3.right * halfGrid,
            basePosition + Vector3.left * halfGrid
        };

        int connectionMask =
            buildLayer.value |
            floorLayer.value |
            coneLayer.value;

        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 halfExtents = i < 2
                ? new Vector3(
                    halfGrid - 0.15f,
                    connectionThickness,
                    connectionThickness)
                : new Vector3(
                    connectionThickness,
                    connectionThickness,
                    halfGrid - 0.15f);

            Collider[] hits = Physics.OverlapBox(
                centers[i],
                halfExtents,
                Quaternion.identity,
                connectionMask,
                QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (IsPreviewCollider(hit) || IsPlayerCollider(hit))
                    continue;

                return true;
            }
        }

        return false;
    }

    private bool IsOverlappingPlayer(Vector3 position)
    {
        if (player == null)
            return false;

        float baseY = position.y - conePivotFromBottom;

        Vector3 center = new Vector3(
            position.x,
            baseY + coneHeight / 2f,
            position.z);

        Vector3 halfExtents = new Vector3(
            Mathf.Max(gridSize / 2f - 0.02f, 0.01f),
            Mathf.Max(coneHeight / 2f - 0.02f, 0.01f),
            Mathf.Max(gridSize / 2f - 0.02f, 0.01f));

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit))
                continue;

            if (IsPlayerCollider(hit))
                return true;
        }

        return false;
    }

    private bool IsOverlappingCone(Vector3 position)
    {
        // 元のネットワーク建築占有判定を維持。
        // 屋根のPivot補正を含めて判定する。
        if (NetworkBuildPiece.Occupied(
            BuildingSystem.BuildType.Cone,
            position,
            Quaternion.Euler(0f, coneRotationY, 0f),
            gridSize,
            conePivotFromBottom,
            false))
        {
            return true;
        }

        float baseY = position.y - conePivotFromBottom;

        Vector3 center = new Vector3(
            position.x,
            baseY + coneHeight / 2f,
            position.z);

        Vector3 halfExtents = new Vector3(
            Mathf.Max(gridSize / 2f - 0.1f, 0.01f),
            Mathf.Max(coneHeight / 2f - 0.05f, 0.05f),
            Mathf.Max(gridSize / 2f - 0.1f, 0.01f));

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            coneLayer,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit) || IsPlayerCollider(hit))
                continue;

            return true;
        }

        return false;
    }

    private bool IsSamePlayerGrid(Vector3 position)
    {
        float playerGridX =
            Mathf.Round(player.position.x / gridSize) * gridSize;

        float playerGridZ =
            Mathf.Round(player.position.z / gridSize) * gridSize;

        return Mathf.Approximately(position.x, playerGridX) &&
               Mathf.Approximately(position.z, playerGridZ);
    }

    private bool IsBehindPlayer(Vector3 position)
    {
        Vector3 playerForward = GetSnappedPlayerForward();
        playerForward.y = 0f;

        if (playerForward.sqrMagnitude <= 0.001f)
            return false;

        Vector3 toBuild = position - player.position;
        toBuild.y = 0f;

        if (toBuild.sqrMagnitude <= 0.01f)
            return false;

        return Vector3.Dot(
            playerForward.normalized,
            toBuild.normalized) < 0f;
    }

    private bool IsPreviewCollider(Collider hit)
    {
        return currentPreview != null &&
               (hit.transform == currentPreview.transform ||
                hit.transform.IsChildOf(currentPreview.transform));
    }

    private bool IsPlayerCollider(Collider hit)
    {
        return player != null &&
               (hit.transform == player ||
                hit.transform.IsChildOf(player));
    }

    private bool IsLayerInMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void SetPreviewMaterial(bool canBuild)
    {
        if (currentPreview == null)
            return;

        Material targetMaterial =
            canBuild ? canBuildMaterial : cannotBuildMaterial;

        if (targetMaterial == null)
            return;

        foreach (Renderer renderer in
                 currentPreview.GetComponentsInChildren<Renderer>())
        {
            renderer.material = targetMaterial;
        }
    }

    private string GetBuildKey(Vector3 position)
    {
        float baseY = position.y - conePivotFromBottom;

        int gridX = Mathf.RoundToInt(position.x / gridSize);
        int gridY = Mathf.RoundToInt(baseY / gridSize);
        int gridZ = Mathf.RoundToInt(position.z / gridSize);

        return gridX + "_" + gridY + "_" + gridZ;
    }
}
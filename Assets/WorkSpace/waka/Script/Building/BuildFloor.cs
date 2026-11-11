using System.Collections.Generic;
using UnityEngine;
using static GameConst;

/// <summary>
/// 床建築を担当するクラス
///
/// ・下向き：Playerの足元
/// ・正面：カメラ方向の隣接マス
/// ・上向き：Playerと同じマスの1段上
/// ・地面または既存建築につながる場合だけ候補を表示
/// ・重複、Playerとの重なりを判定
/// ・生成と建材消費はNetworkBuildControllerへ要求
/// </summary>
public class BuildFloor : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Tooltip("Player本体を設定する")]
    public Transform player;

    [Header("建材消費")]
    [Tooltip("未設定ならPlayer参照の親から取得")]
    public PlayerManager playerManager;

    public BildingMatType materialType;

    [Min(1)]
    public int materialCost = 10;

    [Header("床Prefab")]
    public GameObject floorPrefab;
    public GameObject floorPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    public LayerMask groundLayer;

    [Tooltip("完成した壁・階段・床・屋根など、すべての建築Layer")]
    public LayerMask buildLayer;

    [Tooltip("完成した床だけのLayer")]
    public LayerMask floorLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Tooltip("Colliderから厚さを取得できない場合の値")]
    public float floorThickness = 0.1f;

    [Header("真下判定")]
    [Range(-1f, 0f)]
    public float lookDownThreshold = -0.45f;

    [Header("上向き判定")]
    [Tooltip("この値より上を向くと、現在のマスの1段上を候補にする")]
    [Range(0f, 1f)]
    public float lookUpThreshold = 0.3f;

    [Header("接続判定")]
    public float connectionThickness = 0.15f;

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

        if (floorPreviewPrefab == null)
        {
            Debug.LogError(
                "BuildFloorのFloor Preview Prefabが未設定です",
                this);
            return;
        }

        currentPreview = Instantiate(floorPreviewPrefab);

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
            BuildingSystem.BuildType.Floor,
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

        bool connected = IsFloorConnected(position);
        bool overlapping = IsOverlappingFloor(position);
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
                $"[建築診断・床] " +
                $"接続あり={connected}, " +
                $"床との重複={overlapping}, " +
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
                "BuildFloor: Player Manager、Inventory Manager、" +
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

    /// <summary>
    /// true：候補を表示
    /// false：候補を非表示
    /// canBuild：実際に建築できるか
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
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
            GetPlayerFloorBuildLevel(),
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

        position = GetFloorGridPosition(buildPoint);

        // 接続がなければ候補を表示しない。
        if (!IsFloorConnected(position))
            return false;

        canBuild = true;

        // 同じXZマスなら、真下・真上とも後方判定をしない。
        if (!IsSamePlayerGrid(position) && IsBehindPlayer(position))
            canBuild = false;

        if (builtPositions.Contains(GetBuildKey(position)))
            canBuild = false;

        if (IsOverlappingFloor(position))
            canBuild = false;

        if (IsOverlappingPlayer(position))
            canBuild = false;

        return true;
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

    private Vector3 GetFloorGridPosition(Vector3 buildPoint)
    {
        float x =
            Mathf.Round(buildPoint.x / gridSize) * gridSize;

        float z =
            Mathf.Round(buildPoint.z / gridSize) * gridSize;

        float floorLevel =
            Mathf.Round(buildPoint.y / gridSize) * gridSize;

        return new Vector3(
            x,
            floorLevel + GetActualFloorThickness() / 2f,
            z);
    }

    private float GetPlayerFloorBuildLevel()
    {
        float playerBottomY = player.position.y;

        Collider playerCollider = player.GetComponent<Collider>();

        if (playerCollider != null)
            playerBottomY = playerCollider.bounds.min.y;

        return Mathf.Round(playerBottomY / gridSize) * gridSize;
    }

    private float GetActualFloorThickness()
    {
        if (floorPrefab == null)
            return floorThickness;

        BoxCollider floorCollider =
            floorPrefab.GetComponentInChildren<BoxCollider>();

        if (floorCollider == null)
            return floorThickness;

        float thickness =
            floorCollider.size.y *
            Mathf.Abs(floorCollider.transform.lossyScale.y);

        return thickness <= 0.001f ? floorThickness : thickness;
    }

    private bool IsSamePlayerGrid(Vector3 floorPosition)
    {
        float playerGridX =
            Mathf.Round(player.position.x / gridSize) * gridSize;

        float playerGridZ =
            Mathf.Round(player.position.z / gridSize) * gridSize;

        return Mathf.Approximately(floorPosition.x, playerGridX) &&
               Mathf.Approximately(floorPosition.z, playerGridZ);
    }

    private bool IsFloorConnected(Vector3 floorPosition)
    {
        return IsConnectedToGround(floorPosition) ||
               HasBuildBelow(floorPosition) ||
               HasBuildBeside(floorPosition);
    }

    private bool IsConnectedToGround(Vector3 floorPosition)
    {
        float actualThickness = GetActualFloorThickness();
        float floorLevel = floorPosition.y - actualThickness / 2f;

        // 元の地面階の判定を維持。
        if (floorLevel > 0.1f)
            return false;

        Vector3 checkCenter = floorPosition -
            Vector3.up * (actualThickness / 2f + 0.1f);

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            0.15f,
            gridSize / 2f - 0.15f);

        return Physics.CheckBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    private bool HasBuildBelow(Vector3 floorPosition)
    {
        float actualThickness = GetActualFloorThickness();

        Vector3 checkCenter = floorPosition -
            Vector3.up *
            (actualThickness / 2f + connectionThickness);

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            connectionThickness,
            gridSize / 2f - 0.15f);

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            buildLayer.value | floorLayer.value,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit) || IsPlayerCollider(hit))
                continue;

            return true;
        }

        return false;
    }

    private bool HasBuildBeside(Vector3 floorPosition)
    {
        float halfGrid = gridSize / 2f;

        Vector3[] checkCenters =
        {
            floorPosition + Vector3.forward * halfGrid,
            floorPosition + Vector3.back * halfGrid,
            floorPosition + Vector3.right * halfGrid,
            floorPosition + Vector3.left * halfGrid
        };

        for (int i = 0; i < checkCenters.Length; i++)
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
                checkCenters[i],
                halfExtents,
                Quaternion.identity,
                buildLayer.value | floorLayer.value,
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

    private bool IsOverlappingFloor(Vector3 position)
    {
        float actualThickness = GetActualFloorThickness();

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.1f,
            actualThickness / 2f,
            gridSize / 2f - 0.1f);

        Collider[] hits = Physics.OverlapBox(
            position,
            halfExtents,
            Quaternion.identity,
            floorLayer,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit))
                continue;

            return true;
        }

        return false;
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

    private bool IsOverlappingPlayer(Vector3 position)
    {
        if (player == null)
            return false;

        Physics.SyncTransforms();

        Vector3 halfExtents = new Vector3(
            Mathf.Max(gridSize / 2f, 0.001f),
            Mathf.Max(GetActualFloorThickness() / 2f, 0.001f),
            Mathf.Max(gridSize / 2f, 0.001f));

        Collider[] hits = Physics.OverlapBox(
            position,
            halfExtents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (!IsPreviewCollider(hit) && IsPlayerCollider(hit))
                return true;
        }

        return false;
    }

    private bool IsPlayerCollider(Collider hit)
    {
        return player != null &&
               (hit.transform == player ||
                hit.transform.IsChildOf(player));
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
        int x = Mathf.RoundToInt(position.x * 100f);
        int y = Mathf.RoundToInt(position.y * 100f);
        int z = Mathf.RoundToInt(position.z * 100f);

        return x + "_" + y + "_" + z;
    }
}
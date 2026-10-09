using System.Collections.Generic;
using UnityEngine;
using static GameConst;

/// <summary>
/// 壁建築を担当するクラス
///
/// ・壁をグリッドの境界へ配置
/// ・地面または既存建築につながる場所だけ建築可能
/// ・カメラが空を向いていても前方の接続可能位置を探索
/// ・同じ位置の壁は建築不可
/// ・建築不可能な候補は赤色
/// </summary>
public class BuildWall : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;
    public Transform player;

    [Header("建材消費")]
    [Tooltip("建材を消費するPlayer。未設定ならPlayer参照の親から取得")]
    public PlayerManager playerManager;
    [Tooltip("消費する建材の種類。Inspectorで選択してください")]
    public BildingMatType materialType;
    [Min(1)]
    public int materialCost = 10;

    [Header("壁Prefab")]
    public GameObject wallPrefab;
    public GameObject wallPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面のLayer")]
    public LayerMask groundLayer;

    [Tooltip("Wall・Ramp・Floorなど全建築物のLayer")]
    public LayerMask buildLayer;

    [Tooltip("完成したWallだけのLayer")]
    public LayerMask wallLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("前方候補検索")]
    [Tooltip("Rayが当たらない時に周囲何マスまで候補を探すか")]
    [Range(1, 4)]
    public int candidateSearchRadius = 2;

    [Tooltip("Playerがいる高さより何段上まで探すか")]
    [Range(0, 3)]
    public int candidateSearchHeight = 1;

    [Header("接続判定")]
    public float connectionThickness = 0.15f;

    [Range(0.1f, 0.49f)]
    public float edgeCheckRatio = 0.4f;

    private GameObject currentPreview;

    private PlayerMovement playerMovement;

    // 同じPlayerが使用する生成済みカメラを取得する
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
        Camera generatedCamera = cameraTransform.GetComponent<Camera>();

        if (generatedCamera == null)
        {
            generatedCamera =
                cameraTransform.GetComponentInChildren<Camera>(true);
        }

        if (generatedCamera == null)
        {
            return false;
        }

        playerCamera = generatedCamera;
        return true;
    }

    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// 壁プレビューを生成する
    /// </summary>
    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        if (wallPreviewPrefab == null)
        {
            Debug.LogError(
                "BuildWall：Wall Preview Prefabが設定されていません。",
                this
            );

            return;
        }

        currentPreview =
            Instantiate(wallPreviewPrefab);
    }

    /// <summary>
    /// 壁プレビューを削除する
    /// </summary>
    public void HidePreview()
    {
        if (currentPreview == null)
        {
            return;
        }

        Destroy(currentPreview);
        currentPreview = null;
    }

    /// <summary>
    /// 壁プレビューを更新する
    /// </summary>
    public void UpdatePreview()
    {
        if (currentPreview == null)
        {
            return;
        }

        bool hasCandidate = TryGetBuildPoint(
            out Vector3 position,
            out Quaternion rotation,
            out bool canBuild
        );

        if (!hasCandidate)
        {
            currentPreview.SetActive(false);
            return;
        }

        string key =
            GetBuildKey(position, rotation);

        // 埋まっている候補も赤色で表示し、横へ移動させない。
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        currentPreview.SetActive(true);

        currentPreview.transform.SetPositionAndRotation(
            position,
            rotation
        );

        SetPreviewMaterial(canBuild && HasEnoughMaterials());
    }

    /// <summary>
    /// 壁を実際に建築する
    /// </summary>
    public void Build()
    {
        Physics.SyncTransforms();
        bool hasCandidate = TryGetBuildPoint(
            out Vector3 position,
            out Quaternion rotation,
            out bool canBuild
        );

        if (!hasCandidate || !canBuild)
        {
            return;
        }

        string key =
            GetBuildKey(position, rotation);

        if (builtPositions.Contains(key))
        {
            return;
        }

        if (wallPrefab == null)
        {
            Debug.LogError(
                "BuildWall：Wall Prefabが設定されていません。",
                this
            );

            return;
        }

        // 位置・接続・重複・Prefabの確認がすべて通ってから消費する。
        if (!TryPayMaterials())
        {
            return;
        }

        Instantiate(
            wallPrefab,
            position,
            rotation
        );

        builtPositions.Add(key);
    }

    private bool TryGetMaterialInventory(out InventoryManager inventory)
    {
        inventory = null;
        if (playerManager == null && player != null)
            playerManager = player.GetComponentInParent<PlayerManager>();

        if (playerManager == null) return false;
        // PlayerManagerの初期化済みの参照を使う。
        inventory = playerManager.inventoryManager;
        return inventory != null;
    }

    private bool HasEnoughMaterials()
    {
        InventoryManager inventory;
        return materialCost > 0 &&
            TryGetMaterialInventory(out inventory) &&
            inventory.GetMat(materialType) >= materialCost;
    }

    private bool TryPayMaterials()
    {
        InventoryManager inventory;
        if (materialCost <= 0 || !TryGetMaterialInventory(out inventory))
        {
            Debug.LogWarning(
                "BuildWall: Player ManagerとInventory Managerの参照、およびMaterial Cost（1以上）を確認してください。",
                this);
            return false;
        }

        // 既存の消費APIは不足分だけでも消費するため、必ず先に必要数を確認。
        // 確認と消費の間に待機や別の処理を挟まない。
        if (inventory.GetMat(materialType) < materialCost) return false;

        int consumed;
        return playerManager.TryConsumeBildMat(materialType, materialCost, out consumed)
            && consumed == materialCost;
    }

    /// <summary>
    /// 壁の候補位置を取得する
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild
    )
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        canBuild = false;

        if (!TryResolvePlayerCamera() || player == null)
        {
            return false;
        }

        rotation = GetWallRotation();
        canBuild = true;

        int rayMask =
            groundLayer.value |
            buildLayer.value;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        bool directRayHit =
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                buildDistance,
                rayMask,
                QueryTriggerInteraction.Ignore
            );

        if (directRayHit)
        {
            Vector3 buildPoint;

            if (IsLayerInMask(
                    hit.collider.gameObject.layer,
                    buildLayer))
            {
                buildPoint =
                    hit.point +
                    hit.normal * 0.1f;
            }
            else
            {
                buildPoint = hit.point;
            }

            position = GetWallGridPosition(
                buildPoint,
                rotation
            );

            /*
             * Rayから求めた位置がつながらない場合は、
             * 周囲の接続可能な位置を探す。
             */
            if (!IsWallConnected(position, rotation))
            {
                if (!TryFindConnectedCandidate(
                        rotation,
                        out position))
                {
                    return false;
                }
            }
        }
        else
        {
            /*
             * カメラが空を向いている場合。
             * Player前方にある接続可能なグリッド位置を探す。
             */
            if (!TryFindConnectedCandidate(
                    rotation,
                    out position))
            {
                return false;
            }
        }

        if (IsBehindPlayer(position))
        {
            canBuild = false;
        }

        if (IsOverlappingWall(position, rotation))
        {
            canBuild = false;
        }
        // 候補位置は変更せず、手前に完成壁がある場合だけ設置を拒否。
        // プレビューとBuildの両方がこの判定を通り、建材消費より先に止まる。
        if (IsBlockedByExistingWall(position))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// Player前方のグリッドから、
    /// 地面または既存建築につながる候補を探す
    /// </summary>
    private bool TryFindConnectedCandidate(
        Quaternion rotation,
        out Vector3 bestPosition
    )
    {
        bestPosition = Vector3.zero;

        Vector3 horizontalForward =
            playerCamera.transform.forward;

        horizontalForward.y = 0f;

        // 真上や真下を向いている時はPlayerの向きを使う
        if (horizontalForward.sqrMagnitude <= 0.001f)
        {
            horizontalForward = player.forward;
            horizontalForward.y = 0f;
        }

        if (horizontalForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        horizontalForward.Normalize();

        Vector3 playerGrid =
            SnapToGrid(player.position);

        float playerLevel =
            GetPlayerGridLevel();

        bool found = false;
        float bestScore = float.MaxValue;

        /*
         * Player周辺のグリッドを調べる。
         * 現在の高さと、その上の高さも候補にする。
         */
        for (int height = 0;
             height <= candidateSearchHeight;
             height++)
        {
            float candidateLevel =
                playerLevel +
                height * gridSize;

            for (int x = -candidateSearchRadius;
                 x <= candidateSearchRadius;
                 x++)
            {
                for (int z = -candidateSearchRadius;
                     z <= candidateSearchRadius;
                     z++)
                {
                    Vector3 gridPoint =
                        new Vector3(
                            playerGrid.x + x * gridSize,
                            candidateLevel,
                            playerGrid.z + z * gridSize
                        );

                    Vector3 candidatePosition =
                        GetWallGridPosition(
                            gridPoint,
                            rotation
                        );

                    Vector3 toCandidate =
                        candidatePosition -
                        player.position;

                    Vector3 horizontalToCandidate =
                        toCandidate;

                    horizontalToCandidate.y = 0f;

                    if (horizontalToCandidate.sqrMagnitude <=
                        0.01f)
                    {
                        continue;
                    }

                    /*
                     * Playerの後ろ側にある候補は
                     * 検索対象から外す。
                     */
                    float forwardDot =
                        Vector3.Dot(
                            horizontalForward,
                            horizontalToCandidate.normalized
                        );

                    if (forwardDot < 0f)
                    {
                        continue;
                    }

                    // 接続していない候補は除外
                    if (!IsWallConnected(
                            candidatePosition,
                            rotation))
                    {
                        continue;
                    }

                    // 重複した候補も位置選択には残す。
                    // 正面が埋まっているだけで横の空きマスに飛ばさない。
                    // 選択後、共通の重複・遮蔽判定で建築不可にする。
                    /*
                     * カメラ中央に近い候補を優先する。
                     * 高さも含めて判定するため、
                     * 上を向けば上段の候補が選ばれやすい。
                     */
                    Vector3 cameraToCandidate =
                        candidatePosition -
                        playerCamera.transform.position;

                    float cameraDistance =
                        cameraToCandidate.magnitude;

                    if (cameraDistance <= 0.001f)
                    {
                        continue;
                    }

                    float cameraAlignment =
                        Vector3.Dot(
                            playerCamera.transform.forward,
                            cameraToCandidate.normalized
                        );

                    /*
                     * 小さいほど良いスコア。
                     * カメラの向きとの一致を強く優先する。
                     */
                    float score =
                        (1f - cameraAlignment) * 20f +
                        cameraDistance * 0.1f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPosition = candidatePosition;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Playerの足元があるグリッド階層を取得する
    /// </summary>
    private float GetPlayerGridLevel()
    {
        float playerBottomY =
            player.position.y;

        Collider playerCollider =
            player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            playerCollider =
                player.GetComponentInChildren<Collider>();
        }

        if (playerCollider != null)
        {
            playerBottomY =
                playerCollider.bounds.min.y;
        }

        return Mathf.Floor(
            (playerBottomY + 0.1f) /
            gridSize
        ) * gridSize;
    }

    /// <summary>
    /// 壁をグリッドの境界へ配置する
    /// </summary>
    private Vector3 GetWallGridPosition(
        Vector3 buildPoint,
        Quaternion rotation
    )
    {
        Vector3 position =
            SnapToGrid(buildPoint);

        // 壁のPivotが中心にあるため半分持ち上げる
        position.y += gridSize / 2f;

        int rotationY =
            Mathf.RoundToInt(
                rotation.eulerAngles.y / 90f
            ) * 90;

        rotationY =
            ((rotationY % 360) + 360) %
            360;

        // 壁をマスの境界へ半マスずらす
        switch (rotationY)
        {
            case 0:
                position.z += gridSize / 2f;
                break;

            case 90:
                position.x += gridSize / 2f;
                break;

            case 180:
                position.z -= gridSize / 2f;
                break;

            case 270:
                position.x -= gridSize / 2f;
                break;
        }

        return position;
    }

    /// <summary>
    /// 壁が地面または既存建築につながっているか確認する
    /// </summary>
    private bool IsWallConnected(
        Vector3 position,
        Quaternion rotation
    )
    {
        Vector3 wallRight =
            rotation * Vector3.right;

        wallRight.y = 0f;
        wallRight.x = Mathf.Round(wallRight.x);
        wallRight.z = Mathf.Round(wallRight.z);

        if (wallRight.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        wallRight.Normalize();

        Vector3 bottomEdgeCenter =
            position -
            Vector3.up * (gridSize / 2f);

        Vector3 topEdgeCenter =
            position +
            Vector3.up * (gridSize / 2f);

        Vector3 leftEdgeCenter =
            position -
            wallRight * (gridSize / 2f);

        Vector3 rightEdgeCenter =
            position +
            wallRight * (gridSize / 2f);

        // 下辺が地面につながっている
        if (IsBottomEdgeConnectedToGround(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 下辺が建築物につながっている
        if (IsHorizontalEdgeConnectedToBuild(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 上辺が建築物につながっている
        if (IsHorizontalEdgeConnectedToBuild(
                topEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 左の縦辺が建築物につながっている
        if (IsVerticalEdgeConnectedToBuild(
                leftEdgeCenter))
        {
            return true;
        }

        // 右の縦辺が建築物につながっている
        if (IsVerticalEdgeConnectedToBuild(
                rightEdgeCenter))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 壁の下辺が地面に接しているか確認する
    /// </summary>
    private bool IsBottomEdgeConnectedToGround(
        Vector3 bottomEdgeCenter,
        Vector3 wallRight
    )
    {
        float sideOffset =
            gridSize * edgeCheckRatio;

        Vector3[] checkPoints =
        {
            bottomEdgeCenter -
            wallRight * sideOffset,

            bottomEdgeCenter,

            bottomEdgeCenter +
            wallRight * sideOffset
        };

        foreach (Vector3 point in checkPoints)
        {
            Vector3 rayStart =
                point +
                Vector3.up * 0.25f;

            bool hitGround =
                Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    0.5f,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                );

            if (!hitGround)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 横辺が同じ建築物につながっているか確認する
    /// </summary>
    private bool IsHorizontalEdgeConnectedToBuild(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float offset =
            gridSize * edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter -
            edgeRight * offset,

            edgeCenter,

            edgeCenter +
            edgeRight * offset
        };

        return ArePointsConnectedToSameBuild(
            checkPoints
        );
    }

    /// <summary>
    /// 縦辺が同じ建築物につながっているか確認する
    /// </summary>
    private bool IsVerticalEdgeConnectedToBuild(
        Vector3 edgeCenter
    )
    {
        float offset =
            gridSize * edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter -
            Vector3.up * offset,

            edgeCenter,

            edgeCenter +
            Vector3.up * offset
        };

        return ArePointsConnectedToSameBuild(
            checkPoints
        );
    }

    /// <summary>
    /// 3点すべてが同じ建築物に接しているか確認する
    /// </summary>
    private bool ArePointsConnectedToSameBuild(
        Vector3[] checkPoints
    )
    {
        Transform connectedRoot = null;

        foreach (Vector3 point in checkPoints)
        {
            Collider[] hits =
                Physics.OverlapSphere(
                    point,
                    connectionThickness,
                    buildLayer,
                    QueryTriggerInteraction.Ignore
                );

            Transform foundRoot = null;

            foreach (Collider hit in hits)
            {
                if (IsPreviewCollider(hit))
                {
                    continue;
                }

                if (IsPlayerCollider(hit))
                {
                    continue;
                }

                foundRoot =
                    GetBuildRoot(hit.transform);

                break;
            }

            if (foundRoot == null)
            {
                return false;
            }

            if (connectedRoot == null)
            {
                connectedRoot = foundRoot;
            }
            else if (connectedRoot != foundRoot)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Build Layerの建築物本体を取得する
    /// </summary>
    private Transform GetBuildRoot(
        Transform target
    )
    {
        Transform result = target;

        while (result.parent != null &&
               IsLayerInMask(
                   result.parent.gameObject.layer,
                   buildLayer))
        {
            result = result.parent;
        }

        return result;
    }

    /// <summary>
    /// 完成Wallと重なっているか確認する
    /// </summary>
    private bool IsOverlappingWall(
        Vector3 position,
        Quaternion rotation
    )
    {
        if (wallLayer.value == 0)
        {
            return false;
        }

        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.15f,
                gridSize / 2f - 0.15f,
                0.03f
            );

        Collider[] hits =
            Physics.OverlapBox(
                position,
                halfExtents,
                rotation,
                wallLayer,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit))
            {
                continue;
            }

            if (IsPlayerCollider(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool wallMaskWarningShown;

    /// <summary>
    /// Playerから候補の中心までに完成壁がある場合は設置不可。
    /// 床・坂は遮蔽物に含めず、壁に照準が当たっただけでは拒否しない。
    /// </summary>
    private bool IsBlockedByExistingWall(Vector3 target)
    {
        if (wallLayer.value == 0)
        {
            if (!wallMaskWarningShown)
            {
                Debug.LogError(
                    "BuildWall：Wall Layerに完成壁のLayerを設定してください。",
                    this);
                wallMaskWarningShown = true;
            }
            return true;
        }

        if (player == null)
            return true;

        // 三人称カメラの位置ではなくPlayerの胴体から判定。
        Vector3 origin = player.position + Vector3.up * 1f;
        Collider body = player.GetComponent<Collider>();
        if (body != null && body.enabled)
            origin = body.bounds.center;

        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return false;

        // Rayの始点が壁の内部にあるケースも拒否。
        foreach (Collider wall in Physics.OverlapSphere(
                     origin, 0.005f, wallLayer,
                     QueryTriggerInteraction.Ignore))
        {
            if (!IsPreviewCollider(wall) && !IsPlayerCollider(wall))
                return true;
        }

        // 終点付近の微小な接触は既存の重なり判定に任せる。
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            delta / distance,
            Mathf.Max(0f, distance - 0.01f),
            wallLayer,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (IsPreviewCollider(hit.collider) ||
                IsPlayerCollider(hit.collider))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 候補位置がPlayerより後ろか確認する
    /// </summary>
    private bool IsBehindPlayer(
        Vector3 position
    )
    {
        Vector3 playerForward =
            player.forward;

        playerForward.y = 0f;

        if (playerForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        playerForward.Normalize();

        Vector3 toBuild =
            position -
            player.position;

        toBuild.y = 0f;

        if (toBuild.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        return Vector3.Dot(
            playerForward,
            toBuild.normalized
        ) < 0f;
    }

    /// <summary>
    /// ColliderがPlayerまたはPlayerの子か確認する
    /// </summary>
    private bool IsPlayerCollider(
        Collider hit
    )
    {
        return
            hit.transform == player ||
            hit.transform.IsChildOf(player);
    }

    /// <summary>
    /// Colliderが現在のプレビュー自身か確認する
    /// </summary>
    private bool IsPreviewCollider(
        Collider hit
    )
    {
        if (currentPreview == null)
        {
            return false;
        }

        return
            hit.transform ==
            currentPreview.transform ||
            hit.transform.IsChildOf(
                currentPreview.transform
            );
    }

    /// <summary>
    /// LayerがLayerMaskに含まれているか確認する
    /// </summary>
    private bool IsLayerInMask(
        int layer,
        LayerMask mask
    )
    {
        return
            (mask.value &
             (1 << layer)) != 0;
    }

    /// <summary>
    /// プレビュー色を変更する
    /// </summary>
    private void SetPreviewMaterial(
        bool canBuild
    )
    {
        if (currentPreview == null)
        {
            return;
        }

        Material targetMaterial =
            canBuild
                ? canBuildMaterial
                : cannotBuildMaterial;

        if (targetMaterial == null)
        {
            return;
        }

        Renderer[] renderers =
            currentPreview.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            renderer.material =
                targetMaterial;
        }
    }

    /// <summary>
    /// 座標をグリッドに吸着させる
    /// </summary>
    private Vector3 SnapToGrid(
        Vector3 position
    )
    {
        position.x =
            Mathf.Round(position.x / gridSize) *
            gridSize;

        position.y =
            Mathf.Round(position.y / gridSize) *
            gridSize;

        position.z =
            Mathf.Round(position.z / gridSize) *
            gridSize;

        return position;
    }

    /// <summary>
    /// カメラのY回転を90度単位に丸める
    /// </summary>
    private Quaternion GetWallRotation()
    {
        int snappedY =
            Mathf.RoundToInt(
                playerCamera.transform.eulerAngles.y /
                90f
            ) * 90;

        snappedY =
            ((snappedY % 360) + 360) %
            360;

        return Quaternion.Euler(
            0f,
            snappedY,
            0f
        );
    }

    /// <summary>
    /// 建築済み判定用キー
    /// </summary>
    private string GetBuildKey(
        Vector3 position,
        Quaternion rotation
    )
    {
        int x =
            Mathf.RoundToInt(
                position.x * 100f
            );

        int y =
            Mathf.RoundToInt(
                position.y * 100f
            );

        int z =
            Mathf.RoundToInt(
                position.z * 100f
            );

        int rotationY =
            Mathf.RoundToInt(
                rotation.eulerAngles.y
            );

        return
            x + "_" +
            y + "_" +
            z + "_" +
            rotationY;
    }
}


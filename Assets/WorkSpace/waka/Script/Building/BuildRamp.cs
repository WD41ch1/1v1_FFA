using System.Collections.Generic;
using UnityEngine;
using static GameConst;

/// <summary>
/// 階段建築を担当するクラス
///
/// ・階段をグリッドの中心に配置
/// ・真下を向くとPlayerと同じマスに候補を表示
/// ・Playerに重なる間は赤色で建築不可
/// ・ジャンプして重ならなくなると建築可能
/// ・地面や既存建築につながる場所だけ候補を表示
/// ・既存の階段と重なる場所には建築不可
/// ・Playerより後ろには建築不可
/// </summary>
public class BuildRamp : MonoBehaviour
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

    [Header("階段Prefab")]
    public GameObject rampPrefab;
    public GameObject rampPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面に設定しているLayer")]
    public LayerMask groundLayer;

    [Tooltip("Wall・Ramp・Floorなど、全建築物のLayer")]
    public LayerMask buildLayer;

    [Tooltip("完成したRampだけに設定しているLayer")]
    public LayerMask rampLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("真下への建築")]
    [Tooltip("カメラのY方向がこの値以下なら自分のマスを候補にする")]
    [Range(-1f, 0f)]
    public float lookDownThreshold = -0.45f;

    [Header("Player重なり判定")]
    [Tooltip("Playerの足元がこの高さ以下ならRampに埋まると判断する")]
    public float playerBlockHeight = 1.2f;

    [Header("接続判定")]
    [Tooltip("辺や床との接続を確認する判定の大きさ")]
    public float connectionThickness = 0.15f;

    [Tooltip("辺の中央から左右の確認点までの割合")]
    [Range(0.1f, 0.49f)]
    public float edgeCheckRatio = 0.4f;

    private GameObject currentPreview;

    // このスクリプトから建築した階段を記録する
    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// プレビューを生成する
    /// </summary>
    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        if (rampPreviewPrefab == null)
        {
            Debug.LogError(
                "BuildRamp：Ramp Preview Prefabが設定されていません。",
                this
            );

            return;
        }

        currentPreview = Instantiate(rampPreviewPrefab);
    }

    /// <summary>
    /// プレビューを削除する
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
    /// プレビューの位置・向き・色を更新する
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

        // 地面にも建築物にも接続していない場合は
        // 候補そのものを表示しない
        if (!hasCandidate)
        {
            currentPreview.SetActive(false);
            return;
        }

        currentPreview.SetActive(true);

        currentPreview.transform.SetPositionAndRotation(
            position,
            rotation
        );

        // 建築可能なら通常色、建築不可なら赤色
        SetPreviewMaterial(canBuild && HasEnoughMaterials());
    }

    /// <summary>
    /// 階段を実際に建築する
    /// </summary>
    public void Build()
    {
        bool hasCandidate = TryGetBuildPoint(
            out Vector3 position,
            out Quaternion rotation,
            out bool canBuild
        );

        if (!hasCandidate || !canBuild)
        {
            return;
        }

        string key = GetBuildKey(position, rotation);

        // 同じ位置・同じ向きには建築しない
        if (builtPositions.Contains(key))
        {
            return;
        }

        if (rampPrefab == null)
        {
            Debug.LogError(
                "BuildRamp：Ramp Prefabが設定されていません。",
                this
            );

            return;
        }

        // 配置・接続・重複・Prefabの確認を通った後にだけ消費する。
        if (!TryPayMaterials())
        {
            return;
        }

        Instantiate(rampPrefab, position, rotation);
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
                "BuildRamp: Player ManagerとInventory Managerの参照、およびMaterial Cost（1以上）を確認してください。",
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
    /// 階段の候補位置と建築可能状態を取得する
    ///
    /// 戻り値：
    /// true  = 候補を表示する
    /// false = 候補そのものを表示しない
    ///
    /// canBuild：
    /// true  = 建築可能
    /// false = 赤色候補
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild
    )
    {
        position = Vector3.zero;
        rotation = GetRampRotation();
        canBuild = true;

        if (playerCamera == null || player == null)
        {
            return false;
        }

        Vector3 cameraForward =
            playerCamera.transform.forward;

        Vector3 buildPoint;

        // カメラが真下付近を向いているか
        bool lookingDown =
            cameraForward.y <= lookDownThreshold;

        if (lookingDown)
        {
            /*
             * 真下を向いた場合はRayの当たった場所ではなく、
             * PlayerがいるXZマスを候補にする。
             */
            buildPoint = player.position;

            // Playerの足元の高さを建築グリッドに合わせる
            float playerBottomY = GetPlayerBottomY();

            buildPoint.y =
                Mathf.Round(playerBottomY / gridSize) *
                gridSize;
        }
        else
        {
            int rayMask =
                groundLayer.value |
                buildLayer.value;

            Ray ray = new Ray(
                playerCamera.transform.position,
                cameraForward
            );

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    buildDistance,
                    rayMask,
                    QueryTriggerInteraction.Ignore))
            {
                // 当たった面の少し外側を候補位置にする
                buildPoint =
                    hit.point +
                    hit.normal * 0.1f;
            }
            else
            {
                // 何にも当たらない場合はPlayerの前1マス
                Vector3 forward =
                    GetSnappedForward(rotation);

                buildPoint =
                    player.position +
                    forward * gridSize;

                // 地面へ落とさず、現在いる高さを使用する
                buildPoint.y =
                    Mathf.Round(
                        GetPlayerBottomY() / gridSize
                    ) * gridSize;
            }
        }

        // 階段をグリッドの中心に吸着させる
        position = SnapToGrid(buildPoint);

        /*
         * 地面・床・壁・階段のいずれにも
         * 接続していなければ候補を表示しない。
         */
        if (!IsRampConnected(position, rotation))
        {
            return false;
        }

        // Playerより後ろなら赤色
        if (IsBehindPlayer(position))
        {
            canBuild = false;
        }

        /*
         * Playerが階段に埋まる場合は赤色。
         * 真下候補自体は消さない。
         */
        if (IsPlayerOverlappingRamp(position))
        {
            canBuild = false;
        }

        string key =
            GetBuildKey(position, rotation);

        // 同じ位置・同じ向きの階段がある
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // 別の完成Rampと重なっている
        if (IsOverlappingRamp(position, rotation))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// Rampが地面または既存建築に接続しているか確認する
    /// </summary>
    private bool IsRampConnected(
        Vector3 position,
        Quaternion rotation
    )
    {
        /*
         * Rampの真下に地面・床などがある場合。
         * Playerと同じマスに候補を出す時にも使う。
         */
        if (HasGroundDirectlyUnderRamp(position))
        {
            return true;
        }

        if (HasBuildDirectlyUnderRamp(position))
        {
            return true;
        }

        Vector3 forward =
            GetSnappedForward(rotation);

        Vector3 right =
            GetSnappedRight(rotation);

        // Rampの低い側の辺
        Vector3 lowEdgeCenter =
            position -
            forward * (gridSize / 2f);

        // Rampの高い側の辺
        Vector3 highEdgeCenter =
            position +
            forward * (gridSize / 2f) +
            Vector3.up * gridSize;

        // 低い側の辺が地面につながっている
        if (IsEdgeConnectedToGround(
                lowEdgeCenter,
                right))
        {
            return true;
        }

        // 低い側の辺が建築物につながっている
        if (IsEdgeConnectedToBuild(
                lowEdgeCenter,
                right))
        {
            return true;
        }

        // 高い側の辺が建築物につながっている
        if (IsEdgeConnectedToBuild(
                highEdgeCenter,
                right))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Rampの真下に地面があるか確認する
    /// </summary>
    private bool HasGroundDirectlyUnderRamp(
        Vector3 position
    )
    {
        Vector3 checkCenter =
            position -
            Vector3.up * connectionThickness;

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.1f,
            connectionThickness,
            gridSize / 2f - 0.1f
        );

        return Physics.CheckBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    /// <summary>
    /// Rampの真下にFloorなどの建築物があるか確認する
    /// </summary>
    private bool HasBuildDirectlyUnderRamp(
        Vector3 position
    )
    {
        Vector3 checkCenter =
            position -
            Vector3.up * connectionThickness;

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.1f,
            connectionThickness,
            gridSize / 2f - 0.1f
        );

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            buildLayer,
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

    /// <summary>
    /// 辺の左・中央・右がすべて地面に接しているか確認する
    /// </summary>
    private bool IsEdgeConnectedToGround(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize * edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter - edgeRight * sideOffset,
            edgeCenter,
            edgeCenter + edgeRight * sideOffset
        };

        foreach (Vector3 point in checkPoints)
        {
            bool touchingGround =
                Physics.CheckSphere(
                    point +
                    Vector3.up * connectionThickness,
                    connectionThickness,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                );

            if (!touchingGround)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 辺の左・中央・右がすべて同じ建築物に
    /// 接しているか確認する
    /// </summary>
    private bool IsEdgeConnectedToBuild(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize * edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter - edgeRight * sideOffset,
            edgeCenter,
            edgeCenter + edgeRight * sideOffset
        };

        Transform connectedBuildRoot = null;

        foreach (Vector3 point in checkPoints)
        {
            Collider[] hits =
                Physics.OverlapSphere(
                    point,
                    connectionThickness,
                    buildLayer,
                    QueryTriggerInteraction.Ignore
                );

            Transform foundBuildRoot = null;

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

                foundBuildRoot =
                    GetBuildRoot(hit.transform);

                break;
            }

            // この点が建築物につながっていない
            if (foundBuildRoot == null)
            {
                return false;
            }

            if (connectedBuildRoot == null)
            {
                connectedBuildRoot =
                    foundBuildRoot;
            }
            else if (connectedBuildRoot != foundBuildRoot)
            {
                // 3点が別々の建築物なら接続失敗
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Build Layerに属する建築物の親を取得する
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
    /// 指定されたLayerがLayerMaskに含まれているか確認する
    /// </summary>
    private bool IsLayerInMask(
        int layer,
        LayerMask layerMask
    )
    {
        return
            (layerMask.value &
             (1 << layer)) != 0;
    }

    /// <summary>
    /// 候補位置がPlayerより後ろか確認する
    /// 同じマスの場合は後ろ扱いにしない
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

        // Playerと同じマスは後ろ判定しない
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
    /// Playerが配置予定のRampに埋まるか確認する
    ///
    /// 同じマスでも、ジャンプして足元が
    /// blockedHeightより上なら建築可能になる。
    /// </summary>
    private bool IsPlayerOverlappingRamp(
        Vector3 rampPosition
    )
    {
        Collider playerCollider =
            GetPlayerCollider();

        if (playerCollider == null)
        {
            return false;
        }

        Vector3 playerGrid =
            SnapToGrid(player.position);

        bool sameXZ =
            Mathf.Approximately(
                rampPosition.x,
                playerGrid.x
            ) &&
            Mathf.Approximately(
                rampPosition.z,
                playerGrid.z
            );

        if (!sameXZ)
        {
            return false;
        }

        float playerBottomY =
            playerCollider.bounds.min.y;

        float blockedHeight =
            rampPosition.y +
            playerBlockHeight;

        // 足元が低ければRampに埋まるため建築不可
        return playerBottomY <= blockedHeight;
    }

    /// <summary>
    /// 配置予定位置に完成済みRampが重なっているか確認する
    ///
    /// rampLayerだけを見るので、
    /// 下にあるFloorは重複扱いにならない。
    /// </summary>
    private bool IsOverlappingRamp(
        Vector3 position,
        Quaternion rotation
    )
    {
        // Ramp Layerが未設定ならこの判定を行わない
        if (rampLayer.value == 0)
        {
            return false;
        }

        Vector3 center =
            position +
            Vector3.up * (gridSize / 2f);

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            gridSize / 2f - 0.15f,
            gridSize / 2f - 0.15f
        );

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rotation,
            rampLayer,
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

    /// <summary>
    /// PlayerのColliderを取得する
    /// Colliderが子にある場合にも対応する
    /// </summary>
    private Collider GetPlayerCollider()
    {
        Collider playerCollider =
            player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            playerCollider =
                player.GetComponentInChildren<Collider>();
        }

        return playerCollider;
    }

    /// <summary>
    /// Playerの足元のY座標を取得する
    /// </summary>
    private float GetPlayerBottomY()
    {
        Collider playerCollider =
            GetPlayerCollider();

        if (playerCollider != null)
        {
            return playerCollider.bounds.min.y;
        }

        return player.position.y;
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
            hit.transform == currentPreview.transform ||
            hit.transform.IsChildOf(
                currentPreview.transform
            );
    }

    /// <summary>
    /// Rampの90度単位の前方向を取得する
    /// </summary>
    private Vector3 GetSnappedForward(
        Quaternion rotation
    )
    {
        Vector3 forward =
            rotation * Vector3.forward;

        forward.y = 0f;
        forward.x = Mathf.Round(forward.x);
        forward.z = Mathf.Round(forward.z);

        return forward.normalized;
    }

    /// <summary>
    /// Rampの90度単位の右方向を取得する
    /// </summary>
    private Vector3 GetSnappedRight(
        Quaternion rotation
    )
    {
        Vector3 right =
            rotation * Vector3.right;

        right.y = 0f;
        right.x = Mathf.Round(right.x);
        right.z = Mathf.Round(right.z);

        return right.normalized;
    }

    /// <summary>
    /// 建築可能・不可能でプレビュー色を変更する
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
    private Quaternion GetRampRotation()
    {
        float cameraY =
            playerCamera.transform.eulerAngles.y;

        float snappedY =
            Mathf.Round(cameraY / 90f) *
            90f;

        return Quaternion.Euler(
            0f,
            snappedY,
            0f
        );
    }

    /// <summary>
    /// 建築済み判定用のキーを作る
    /// </summary>
    private string GetBuildKey(
        Vector3 position,
        Quaternion rotation
    )
    {
        // 小数点以下を丸めて整数化することで、
        int x =
            Mathf.RoundToInt(position.x * 100f);

        int y =
            Mathf.RoundToInt(position.y * 100f);

        int z =
            Mathf.RoundToInt(position.z * 100f);

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
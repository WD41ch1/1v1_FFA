using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 階段建築を担当するクラス
///
/// ・グリッドに沿って配置する
/// ・Playerより後ろには建築できない
/// ・Playerと重なる場合は建築できない
/// ・ジャンプしてPlayerと重ならなければ足元にも建築できる
/// ・既存建築と内部が重なる場合は建築できない
/// ・地面、または既存建築の辺と正しく接続している場合だけ建築できる
/// ・角だけ触れている状態や、何もない空中には建築できない
/// </summary>
public class BuildRamp : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;
    public Transform player;

    [Header("階段Prefab")]
    public GameObject rampPrefab;
    public GameObject rampPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面に設定しているLayerを選択する")]
    public LayerMask groundLayer;

    [Tooltip("WallやRampなどの建築物に設定しているLayerを選択する")]
    public LayerMask buildLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("Player重なり判定")]
    [Tooltip("Playerの足元がこの高さより上なら、ジャンプで避けていると判断する")]
    public float playerBlockHeight = 1.2f;

    [Header("辺の接続判定")]
    [Tooltip("辺の接続を確認する球判定の大きさ")]
    public float connectionThickness = 0.15f;

    [Tooltip("辺の中央から左右の確認点までの割合。0.4なら幅4の時に左右1.6")]
    [Range(0.1f, 0.49f)]
    public float edgeCheckRatio = 0.4f;

    private GameObject currentPreview;

    // このBuildRampから建築したRampの位置を記録する
    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// Rampのプレビューを生成する
    /// </summary>
    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(rampPreviewPrefab);
    }

    /// <summary>
    /// Rampのプレビューを削除する
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
    /// プレビューの位置・向き・色を毎フレーム更新する
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

        // 地面にも建築物にも辺が接続していない場合は、
        // プレビュー自体を表示しない
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

        // 建築可能なら通常色、建築不可能なら赤色
        SetPreviewMaterial(canBuild);
    }

    /// <summary>
    /// Rampを実際に建築する
    /// </summary>
    public void Build()
    {
        bool hasCandidate = TryGetBuildPoint(
            out Vector3 position,
            out Quaternion rotation,
            out bool canBuild
        );

        // 候補がない、または建築不可能なら何もしない
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

        Instantiate(rampPrefab, position, rotation);

        builtPositions.Add(key);
    }

    /// <summary>
    /// 建築候補の位置と向きを取得する
    ///
    /// 戻り値：
    /// true  = プレビュー候補を表示する
    /// false = プレビュー候補自体を表示しない
    ///
    /// canBuild：
    /// true  = 実際に建築可能
    /// false = 赤プレビューとして表示
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

        // RayはGroundとBuildだけに当てる
        int rayMask =
            groundLayer.value |
            buildLayer.value;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Vector3 buildPoint;

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                buildDistance,
                rayMask,
                QueryTriggerInteraction.Ignore))
        {
            // 当たった面の少し外側を候補位置の基準にする
            buildPoint =
                hit.point +
                hit.normal * 0.1f;
        }
        else
        {
            // Rayが何にも当たらなかった場合は、
            // Playerの正面1マスを候補にする
            Vector3 forward =
                GetSnappedForward(rotation);

            buildPoint =
                player.position +
                forward * gridSize;

            // Playerの高さをグリッドに合わせる
            buildPoint.y =
                Mathf.Round(
                    player.position.y / gridSize
                ) * gridSize;
        }

        // XYZをグリッドに吸着させる
        position = SnapToGrid(buildPoint);

        /*
         * 地面、または既存建築の辺と
         * 正しく接続していない場合は、
         * プレビュー候補自体を表示しない
         */
        if (!IsEdgeConnected(position, rotation))
        {
            return false;
        }

        // Playerより後ろなら建築不可
        if (IsBehindPlayer(position))
        {
            canBuild = false;
        }

        // Playerの体がRampに埋まるなら建築不可
        if (IsPlayerOverlappingRamp(position))
        {
            canBuild = false;
        }

        string key = GetBuildKey(position, rotation);

        // 同じ位置・同じ向きのRampがすでにある
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // 既存建築とRampの内部が重なっている
        if (IsOverlappingBuild(position, rotation))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// Rampの辺が、地面または既存建築の辺と
    /// 幅全体で正しく接続しているか確認する
    /// </summary>
    private bool IsEdgeConnected(
        Vector3 position,
        Quaternion rotation
    )
    {
        Vector3 forward =
            GetSnappedForward(rotation);

        Vector3 right =
            GetSnappedRight(rotation);

        /*
         * Rampを横から見たイメージ
         *
         *                 上端
         *              ─────────
         *             ／
         *            ／
         *  ─────────
         *     下端
         *
         * positionはRampPrefabの地面側Pivotを想定
         */

        // Rampの下端中央
        Vector3 lowEdgeCenter =
            position -
            forward * (gridSize / 2f);

        // Rampの上端中央
        Vector3 highEdgeCenter =
            position +
            forward * (gridSize / 2f) +
            Vector3.up * gridSize;

        // 下端の左・中央・右が地面に接していればOK
        if (IsEdgeConnectedToGround(
                lowEdgeCenter,
                right))
        {
            return true;
        }

        // 下端の左・中央・右が同じ建築物に接していればOK
        if (IsEdgeConnectedToBuild(
                lowEdgeCenter,
                right))
        {
            return true;
        }

        // 上端の左・中央・右が同じ建築物に接していればOK
        if (IsEdgeConnectedToBuild(
                highEdgeCenter,
                right))
        {
            return true;
        }

        // どの辺も正しく接続していない
        return false;
    }

    /// <summary>
    /// Rampの辺の左・中央・右が、
    /// すべて地面に接しているか確認する
    /// </summary>
    private bool IsEdgeConnectedToGround(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize *
            edgeCheckRatio;

        // 辺の左・中央・右の3点
        Vector3[] checkPoints =
        {
            edgeCenter - edgeRight * sideOffset,
            edgeCenter,
            edgeCenter + edgeRight * sideOffset
        };

        foreach (Vector3 point in checkPoints)
        {
            // 地面の少し上を中心に球判定する
            Vector3 checkCenter =
                point +
                Vector3.up * connectionThickness;

            bool touchingGround =
                Physics.CheckSphere(
                    checkCenter,
                    connectionThickness,
                    groundLayer,
                    QueryTriggerInteraction.Ignore
                );

            // 1点でも地面に触れていないなら、
            // 辺全体は接続していない
            if (!touchingGround)
            {
                return false;
            }
        }

        // 左・中央・右すべて地面に接している
        return true;
    }

    /// <summary>
    /// Rampの辺の左・中央・右が、
    /// すべて同じ建築物に接しているか確認する
    /// </summary>
    private bool IsEdgeConnectedToBuild(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize *
            edgeCheckRatio;

        // 辺の左・中央・右の3点
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
                // プレビュー自身は無視
                if (IsPreviewCollider(hit))
                {
                    continue;
                }

                // Playerは無視
                if (IsPlayerCollider(hit))
                {
                    continue;
                }

                // Colliderが付いている建築物の一番上の親を取得
                foundBuildRoot =
                    GetBuildRoot(hit.transform);

                break;
            }

            // この確認点に建築物が接していない
            if (foundBuildRoot == null)
            {
                return false;
            }

            // 最初の確認点で見つけた建築物を記録
            if (connectedBuildRoot == null)
            {
                connectedBuildRoot =
                    foundBuildRoot;
            }
            // 左・中央・右が別々の建築物なら、
            // 1本の辺として接続していない
            else if (connectedBuildRoot != foundBuildRoot)
            {
                return false;
            }
        }

        // 左・中央・右が同じ建築物に接している
        return true;
    }

    /// <summary>
    /// Colliderが属している建築物の親を取得する
    /// Build Layerではない親まで上へたどらない
    /// </summary>
    private Transform GetBuildRoot(Transform target)
    {
        Transform result = target;

        // 親もBuild Layerなら、その親までたどる
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
    /// 建築候補がPlayerより後ろにあるか確認する
    /// </summary>
    private bool IsBehindPlayer(Vector3 position)
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

        // 同じマスなら後ろ判定を行わない
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
    /// Playerの体が配置予定Rampに埋まるか確認する
    /// ジャンプして足元が十分上なら建築可能
    /// </summary>
    private bool IsPlayerOverlappingRamp(
        Vector3 rampPosition
    )
    {
        Collider playerCollider =
            player.GetComponent<Collider>();

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

        // PlayerとRampのXZマスが違う
        if (!sameXZ)
        {
            return false;
        }

        float playerBottomY =
            playerCollider.bounds.min.y;

        float blockedHeight =
            rampPosition.y +
            playerBlockHeight;

        // Playerの足元が低い場合はRampに埋まる
        return playerBottomY <= blockedHeight;
    }

    /// <summary>
    /// 配置予定Rampの内部に既存建築が重なっているか確認する
    /// 辺同士が接しているだけなら重なり扱いにしない
    /// </summary>
    private bool IsOverlappingBuild(
        Vector3 position,
        Quaternion rotation
    )
    {
        Vector3 center =
            position +
            Vector3.up * (gridSize / 2f);

        // 1マスより少し小さくして、
        // 辺だけ接している隣の建築を誤検出しない
        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.15f,
                gridSize / 2f - 0.15f,
                gridSize / 2f - 0.15f
            );

        Collider[] hits =
            Physics.OverlapBox(
                center,
                halfExtents,
                rotation,
                buildLayer,
                QueryTriggerInteraction.Ignore
            );

        foreach (Collider hit in hits)
        {
            // プレビュー自身は無視
            if (IsPreviewCollider(hit))
            {
                continue;
            }

            // Playerは別の判定で見るので無視
            if (IsPlayerCollider(hit))
            {
                continue;
            }

            // Rampの内部に建築物がある
            return true;
        }

        return false;
    }

    /// <summary>
    /// ColliderがPlayer自身、またはPlayerの子か確認する
    /// </summary>
    private bool IsPlayerCollider(Collider hit)
    {
        return
            hit.transform == player ||
            hit.transform.IsChildOf(player);
    }

    /// <summary>
    /// Colliderが現在のプレビュー自身か確認する
    /// </summary>
    private bool IsPreviewCollider(Collider hit)
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
            rotation *
            Vector3.forward;

        forward.y = 0f;

        forward.x =
            Mathf.Round(forward.x);

        forward.z =
            Mathf.Round(forward.z);

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
            rotation *
            Vector3.right;

        right.y = 0f;

        right.x =
            Mathf.Round(right.x);

        right.z =
            Mathf.Round(right.z);

        return right.normalized;
    }

    /// <summary>
    /// 建築可能・不可能でプレビュー色を変更する
    /// </summary>
    private void SetPreviewMaterial(bool canBuild)
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
    private Vector3 SnapToGrid(Vector3 position)
    {
        position.x =
            Mathf.Round(
                position.x / gridSize
            ) * gridSize;

        position.y =
            Mathf.Round(
                position.y / gridSize
            ) * gridSize;

        position.z =
            Mathf.Round(
                position.z / gridSize
            ) * gridSize;

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
            Mathf.Round(
                cameraY / 90f
            ) * 90f;

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
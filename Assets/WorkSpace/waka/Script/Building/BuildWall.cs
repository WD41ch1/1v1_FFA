using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁建築を担当するクラス
///
/// ・地面では通常どおり建築できる
/// ・高所ではPlayerがいる階層に候補を出す
/// ・既存建築と辺がつながる場合は空中でも建築できる
/// ・何にもつながらない空中建築は禁止
/// ・建築できない場所は赤く表示する
/// </summary>
public class BuildWall : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Tooltip("Player本体を設定する")]
    public Transform player;

    [Header("壁Prefab")]
    public GameObject wallPrefab;
    public GameObject wallPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面に設定しているLayer")]
    public LayerMask groundLayer;

    [Tooltip("WallやRampなどに設定しているLayer")]
    public LayerMask buildLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("接続判定")]
    [Tooltip("辺の接続を確認する球判定の大きさ")]
    public float connectionThickness = 0.15f;

    [Tooltip("辺の左・中央・右を確認する位置")]
    [Range(0.1f, 0.49f)]
    public float edgeCheckRatio = 0.4f;

    private GameObject currentPreview;

    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// 壁のプレビューを生成する
    /// </summary>
    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(wallPreviewPrefab);
    }

    /// <summary>
    /// 壁のプレビューを削除する
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

        // 地面にも建築物にも接続していない場合は候補を消す
        if (!hasCandidate)
        {
            currentPreview.SetActive(false);
            return;
        }

        string key = GetBuildKey(position, rotation);

        // すでに同じ壁がある場合は候補を消す
        if (builtPositions.Contains(key))
        {
            currentPreview.SetActive(false);
            return;
        }

        currentPreview.SetActive(true);
        currentPreview.transform.SetPositionAndRotation(
            position,
            rotation
        );

        SetPreviewMaterial(canBuild);
    }

    /// <summary>
    /// 壁を建築する
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

        if (builtPositions.Contains(key))
        {
            return;
        }

        Instantiate(wallPrefab, position, rotation);
        builtPositions.Add(key);
    }

    /// <summary>
    /// 壁の候補位置と建築可能状態を取得する
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild
    )
    {
        position = Vector3.zero;
        rotation = GetWallRotation();
        canBuild = true;

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
            // 建築物に当たった場合は面の少し外側へ出す
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
                // 地面に当たった場合は当たった位置を使う
                buildPoint = hit.point;
            }
        }
        else
        {
            // Rayが当たらない場合はPlayerの前へ候補を出す
            Vector3 forward =
                playerCamera.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            forward.Normalize();

            buildPoint =
                player.position +
                forward * buildDistance;

            /*
             * 重要：
             * 下の地面まで候補を落とさず、
             * Playerの足元から現在いる階層を求める。
             *
             * Playerの足元が0付近 → Y=0
             * Playerの足元が4付近 → Y=4
             * Playerの足元が8付近 → Y=8
             */
            buildPoint.y = GetPlayerGridLevel();
        }

        // 壁をグリッドの境界へ配置
        position = GetWallGridPosition(
            buildPoint,
            rotation
        );

        /*
         * 地面または既存建築に接続していなければ、
         * 空中建築になるため候補を表示しない。
         */
        if (!IsWallConnected(position, rotation))
        {
            return false;
        }

        // カメラの後ろ側では建築不可
        if (IsBehindCamera(position))
        {
            canBuild = false;
        }

        // 既存建築と壁の内部が重なる場合は建築不可
        if (IsOverlappingBuild(position, rotation))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// Playerの足元を基準に現在のグリッド階層を取得する
    /// </summary>
    private float GetPlayerGridLevel()
    {
        float playerBottomY = player.position.y;

        Collider playerCollider =
            player.GetComponent<Collider>();

        if (playerCollider != null)
        {
            // Transformの中心ではなく足元を使用する
            playerBottomY = playerCollider.bounds.min.y;
        }

        /*
         * Floorを使うことで、階段の途中にいても
         * 不必要に1段上へ丸められるのを防ぐ。
         */
        return Mathf.Floor(
            (playerBottomY + 0.1f) / gridSize
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

        // 壁のPivotが中心なので半分上げる
        position.y += gridSize / 2f;

        float rotationY =
            Mathf.Round(
                rotation.eulerAngles.y / 90f
            ) * 90f;

        if (rotationY >= 360f)
        {
            rotationY = 0f;
        }

        // 壁をマスの境界へ半マスずらす
        if (rotationY == 0f)
        {
            position.z += gridSize / 2f;
        }
        else if (rotationY == 90f)
        {
            position.x += gridSize / 2f;
        }
        else if (rotationY == 180f)
        {
            position.z -= gridSize / 2f;
        }
        else if (rotationY == 270f)
        {
            position.x -= gridSize / 2f;
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

        // 壁の下辺が地面に接続している
        if (IsBottomEdgeConnectedToGround(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 壁の下辺が既存建築に接続している
        if (IsHorizontalEdgeConnectedToBuild(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 壁の上辺が既存建築に接続している
        if (IsHorizontalEdgeConnectedToBuild(
                topEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 壁の左辺が既存建築に接続している
        if (IsVerticalEdgeConnectedToBuild(
                leftEdgeCenter))
        {
            return true;
        }

        // 壁の右辺が既存建築に接続している
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
    /// <summary>
    /// 壁の下辺が地面に接しているか確認する。
    /// 左・中央・右の3点から真下へRayを飛ばすため、
    /// 壁の向きが0・90・180・270度のどれでも同じように判定できる。
    /// </summary>
    private bool IsBottomEdgeConnectedToGround(
        Vector3 bottomEdgeCenter,
        Vector3 wallRight
    )
    {
        float sideOffset =
            gridSize * edgeCheckRatio;

        // 壁の下辺にある左・中央・右の3点
        Vector3[] checkPoints =
        {
        bottomEdgeCenter - wallRight * sideOffset,
        bottomEdgeCenter,
        bottomEdgeCenter + wallRight * sideOffset
    };

        foreach (Vector3 point in checkPoints)
        {
            // 地面の少し上から下へRayを飛ばす
            Vector3 rayStart =
                point + Vector3.up * 0.25f;

            bool hitGround = Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit groundHit,
                0.5f,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            // 3点のうち1点でも地面がなければ接続していない
            if (!hitGround)
            {
                return false;
            }
        }

        // 左・中央・右すべてが地面に接している
        return true;
    }

    /// <summary>
    /// 横辺の左・中央・右が同じ建築物に接続しているか確認する
    /// </summary>
    private bool IsHorizontalEdgeConnectedToBuild(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float offset =
            gridSize * edgeCheckRatio;

        Vector3[] points =
        {
            edgeCenter - edgeRight * offset,
            edgeCenter,
            edgeCenter + edgeRight * offset
        };

        return ArePointsConnectedToSameBuild(points);
    }

    /// <summary>
    /// 縦辺の下・中央・上が同じ建築物に接続しているか確認する
    /// </summary>
    private bool IsVerticalEdgeConnectedToBuild(
        Vector3 edgeCenter
    )
    {
        float offset =
            gridSize * edgeCheckRatio;

        Vector3[] points =
        {
            edgeCenter - Vector3.up * offset,
            edgeCenter,
            edgeCenter + Vector3.up * offset
        };

        return ArePointsConnectedToSameBuild(points);
    }

    /// <summary>
    /// すべての確認点が同じ建築物に接続しているか確認する
    /// </summary>
    private bool ArePointsConnectedToSameBuild(
        Vector3[] points
    )
    {
        Transform connectedRoot = null;

        foreach (Vector3 point in points)
        {
            Collider[] hits = Physics.OverlapSphere(
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

                foundRoot =
                    GetBuildRoot(hit.transform);

                break;
            }

            // 1点でも接続していなければ失敗
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
    /// Build Layerに属するPrefabの親を取得する
    /// </summary>
    private Transform GetBuildRoot(Transform target)
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
    /// 壁の内部に既存建築が重なっているか確認する
    /// </summary>
    private bool IsOverlappingBuild(
        Vector3 position,
        Quaternion rotation
    )
    {
        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            gridSize / 2f - 0.15f,
            0.05f
        );

        Collider[] hits = Physics.OverlapBox(
            position,
            halfExtents,
            rotation,
            buildLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            if (IsPreviewCollider(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 候補位置がカメラの後ろか確認する
    /// </summary>
    private bool IsBehindCamera(Vector3 position)
    {
        Vector3 forward =
            playerCamera.transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        forward.Normalize();

        Vector3 toBuild =
            position -
            playerCamera.transform.position;

        toBuild.y = 0f;

        if (toBuild.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        return Vector3.Dot(
            forward,
            toBuild.normalized
        ) < 0f;
    }

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

    private bool IsLayerInMask(
        int layer,
        LayerMask mask
    )
    {
        return
            (mask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// プレビューの色を変更する
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
            renderer.material = targetMaterial;
        }
    }

    /// <summary>
    /// 座標をグリッドに合わせる
    /// </summary>
    private Vector3 SnapToGrid(Vector3 position)
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
    /// カメラの向きを90度単位に丸める
    /// </summary>
    private Quaternion GetWallRotation()
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
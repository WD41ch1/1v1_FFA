using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁建築を担当するクラス
///
/// ・グリッドの境界に壁を配置する
/// ・地面または既存建築につながる場所だけ建築可能
/// ・階段の横にも壁を接続できる
/// ・空中に浮く壁は禁止
/// ・建築不可能な場所は赤プレビュー
/// </summary>
public class BuildWall : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Header("壁Prefab")]
    public GameObject wallPrefab;
    public GameObject wallPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面に設定しているLayer")]
    public LayerMask groundLayer;

    [Tooltip("WallやRampなどの建築物に設定しているLayer")]
    public LayerMask buildLayer;

    [Header("設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("接続判定")]
    [Tooltip("辺の接続を確認する球判定の大きさ")]
    public float connectionThickness = 0.15f;

    [Tooltip("辺の中央から上下・左右の確認点までの割合")]
    [Range(0.1f, 0.49f)]
    public float edgeCheckRatio = 0.4f;

    private GameObject currentPreview;

    // このBuildWallから建築した壁の位置を記録
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

        currentPreview = Instantiate(wallPreviewPrefab);
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
    /// 壁プレビューの位置・向き・色を更新する
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

        // 地面にも既存建築にもつながっていない場合は
        // プレビュー自体を消す
        if (!hasCandidate)
        {
            currentPreview.SetActive(false);
            return;
        }

        string key = GetBuildKey(position, rotation);

        // すでに同じ位置・向きに壁がある場合は
        // 赤ではなく候補自体を消す
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
    /// 壁を実際に建築する
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
    /// 壁の候補位置と向きを取得する
    ///
    /// 戻り値：
    /// true  = 候補を表示できる
    /// false = 候補自体を表示しない
    ///
    /// canBuild：
    /// true  = 建築可能
    /// false = 赤プレビュー
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
            // 階段や壁に当たった時は、
            // 当たった面の少し外側を候補位置にする
            buildPoint =
                hit.point +
                hit.normal * 0.1f;
        }
        else
        {
            // Rayが何にも当たらない場合は、
            // カメラの水平前方向に候補を出す
            Vector3 forward =
                playerCamera.transform.forward;

            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            forward.Normalize();

            buildPoint =
                playerCamera.transform.position +
                forward * buildDistance;

            // 現在の高さをグリッドに合わせる
            buildPoint.y =
                Mathf.Round(
                    playerCamera.transform.position.y /
                    gridSize
                ) * gridSize;
        }

        // 壁をグリッドの境界に配置
        position =
            GetWallGridPosition(
                buildPoint,
                rotation
            );

        /*
         * 地面または既存建築と辺がつながっていないなら、
         * 空中に浮く壁になるので候補自体を表示しない
         */
        if (!IsWallEdgeConnected(position, rotation))
        {
            return false;
        }

        // カメラより後ろ側なら建築不可
        if (IsBehindCamera(position))
        {
            canBuild = false;
        }

        // 既存建築と内部が重なっているなら建築不可
        if (IsOverlappingBuild(position, rotation))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// 壁をグリッドの境界に配置する
    /// </summary>
    private Vector3 GetWallGridPosition(
        Vector3 buildPoint,
        Quaternion rotation
    )
    {
        Vector3 position =
            SnapToGrid(buildPoint);

        // 壁PrefabのPivotが中心にあるので、
        // 高さの半分だけ上げる
        position.y += gridSize / 2f;

        float rotationY =
            Mathf.Round(
                rotation.eulerAngles.y / 90f
            ) * 90f;

        if (rotationY >= 360f)
        {
            rotationY = 0f;
        }

        // 壁をグリッドの中心ではなく、
        // マスの境界へ半マスずらす
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
    /// 壁の下辺・上辺・左右辺のどこかが、
    /// 地面または既存建築と幅全体でつながっているか確認する
    /// </summary>
    private bool IsWallEdgeConnected(
        Vector3 position,
        Quaternion rotation
    )
    {
        // 壁の横方向
        Vector3 wallRight =
            rotation *
            Vector3.right;

        wallRight.y = 0f;

        wallRight.x =
            Mathf.Round(wallRight.x);

        wallRight.z =
            Mathf.Round(wallRight.z);

        wallRight.Normalize();

        // 壁の下辺・上辺
        Vector3 bottomEdgeCenter =
            position -
            Vector3.up * (gridSize / 2f);

        Vector3 topEdgeCenter =
            position +
            Vector3.up * (gridSize / 2f);

        // 壁の左右の縦辺
        Vector3 leftEdgeCenter =
            position -
            wallRight * (gridSize / 2f);

        Vector3 rightEdgeCenter =
            position +
            wallRight * (gridSize / 2f);

        // 下辺が地面に接続
        if (IsHorizontalEdgeConnectedToGround(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 下辺が建築物に接続
        if (IsHorizontalEdgeConnectedToBuild(
                bottomEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 上辺が建築物に接続
        if (IsHorizontalEdgeConnectedToBuild(
                topEdgeCenter,
                wallRight))
        {
            return true;
        }

        // 左の縦辺が建築物に接続
        if (IsVerticalEdgeConnectedToBuild(
                leftEdgeCenter))
        {
            return true;
        }

        // 右の縦辺が建築物に接続
        if (IsVerticalEdgeConnectedToBuild(
                rightEdgeCenter))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 壁の横辺の左・中央・右が
    /// すべて地面に接しているか確認する
    /// </summary>
    private bool IsHorizontalEdgeConnectedToGround(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize *
            edgeCheckRatio;

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
    /// 壁の横辺の左・中央・右が
    /// すべて同じ建築物に接しているか確認する
    /// </summary>
    private bool IsHorizontalEdgeConnectedToBuild(
        Vector3 edgeCenter,
        Vector3 edgeRight
    )
    {
        float sideOffset =
            gridSize *
            edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter - edgeRight * sideOffset,
            edgeCenter,
            edgeCenter + edgeRight * sideOffset
        };

        return ArePointsConnectedToSameBuild(
            checkPoints
        );
    }

    /// <summary>
    /// 壁の縦辺の下・中央・上が
    /// すべて同じ建築物に接しているか確認する
    /// </summary>
    private bool IsVerticalEdgeConnectedToBuild(
        Vector3 edgeCenter
    )
    {
        float verticalOffset =
            gridSize *
            edgeCheckRatio;

        Vector3[] checkPoints =
        {
            edgeCenter - Vector3.up * verticalOffset,
            edgeCenter,
            edgeCenter + Vector3.up * verticalOffset
        };

        return ArePointsConnectedToSameBuild(
            checkPoints
        );
    }

    /// <summary>
    /// 指定した3点がすべて同じ建築物に接しているか確認する
    /// </summary>
    private bool ArePointsConnectedToSameBuild(
        Vector3[] checkPoints
    )
    {
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
                // 現在のPreviewは接続元にしない
                if (IsPreviewCollider(hit))
                {
                    continue;
                }

                foundBuildRoot =
                    GetBuildRoot(
                        hit.transform
                    );

                break;
            }

            // 1点でも建築物に触れていない
            if (foundBuildRoot == null)
            {
                return false;
            }

            if (connectedBuildRoot == null)
            {
                connectedBuildRoot =
                    foundBuildRoot;
            }
            // 3点が別々の建築物なら
            // 1本の辺としては接続していない
            else if (connectedBuildRoot != foundBuildRoot)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Build Layerに属する建築物の親を取得する
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
    /// 指定LayerがLayerMaskに含まれているか確認する
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
    /// 壁候補がカメラより後ろにあるか確認する
    /// </summary>
    private bool IsBehindCamera(Vector3 position)
    {
        Vector3 cameraForward =
            playerCamera.transform.forward;

        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        cameraForward.Normalize();

        Vector3 toBuild =
            position -
            playerCamera.transform.position;

        toBuild.y = 0f;

        if (toBuild.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        return Vector3.Dot(
            cameraForward,
            toBuild.normalized
        ) < 0f;
    }

    /// <summary>
    /// 壁の内部に既存建築が重なっているか確認する
    /// 辺が接しているだけなら重なり扱いにしない
    /// </summary>
    private bool IsOverlappingBuild(
        Vector3 position,
        Quaternion rotation
    )
    {
        /*
         * 壁は薄いので、1マスの箱ではなく
         * 壁の幅・高さ・薄さに近い箱で判定する
         */
        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.15f,
                gridSize / 2f - 0.15f,
                0.05f
            );

        Collider[] hits =
            Physics.OverlapBox(
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
    /// 現在のプレビュー自身のColliderか確認する
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
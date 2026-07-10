using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 階段建築を担当するクラス
///
/// ・グリッドに沿って配置
/// ・Playerより後ろには建築不可
/// ・Playerと重なる場合は建築不可
/// ・地面または既存建築につながっている場合だけ建築可能
/// ・何にもつながっていない空中建築は禁止
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
    [Tooltip("地面のLayerを選択する")]
    public LayerMask groundLayer;

    [Tooltip("WallやRampなど、建築物のLayerを選択する")]
    public LayerMask buildLayer;

    [Header("設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("Player重なり判定")]
    public float playerBlockHeight = 1.2f;

    [Header("接続判定")]
    [Tooltip("辺の接続を確認する判定の厚さ")]
    public float connectionThickness = 0.15f;

    private GameObject currentPreview;

    // このBuildRampから建築した位置を記録する
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

        // 何にもつながっていない空中なら
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

        // 建築可能なら通常色、建築不可なら赤
        SetPreviewMaterial(canBuild);
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

        // 候補がない、または建築できない場合
        if (!hasCandidate || !canBuild)
        {
            return;
        }

        string key = GetBuildKey(position, rotation);

        // 同じ場所・同じ向きには建てない
        if (builtPositions.Contains(key))
        {
            return;
        }

        Instantiate(rampPrefab, position, rotation);

        builtPositions.Add(key);
    }

    /// <summary>
    /// 建築候補位置を取得する
    ///
    /// 戻り値：
    /// true  = 候補を表示する
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
        rotation = GetRampRotation();
        canBuild = true;

        // GroundとBuildだけをRayの対象にする
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
            // 当たった面の少し外側を建築基準にする
            buildPoint =
                hit.point +
                hit.normal * 0.1f;
        }
        else
        {
            // Rayが何にも当たらなかった場合は
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

        // XYZをグリッドに合わせる
        position = SnapToGrid(buildPoint);

        /*
         * 地面にも既存建築にも接続していない場合、
         * 空中建築になるため候補自体を表示しない
         */
        if (!IsConnected(position, rotation))
        {
            return false;
        }

        // Playerの後ろ側なら建築不可
        if (IsBehindPlayer(position))
        {
            canBuild = false;
        }

        // Playerに埋まる場合は建築不可
        if (IsPlayerOverlappingRamp(position))
        {
            canBuild = false;
        }

        string key = GetBuildKey(
            position,
            rotation
        );

        // 同じ場所・同じ向きのRampがある
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // 既存建築と内部が重なっている
        if (IsOverlappingBuild(position, rotation))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// 地面または既存建築と、
    /// Rampの辺・面がつながっているか確認する
    /// </summary>
    private bool IsConnected(
        Vector3 position,
        Quaternion rotation
    )
    {
        // 地面と接していれば建築可能
        if (IsConnectedToGround(position))
        {
            return true;
        }

        // Rampの前・右方向
        Vector3 forward =
            GetSnappedForward(rotation);

        Vector3 right =
            GetSnappedRight(rotation);

        /*
         * Rampは1マスの箱の中に入る前提。
         *
         * 確認する場所：
         * ・底面
         * ・前面
         * ・後面
         * ・右面
         * ・左面
         * ・Rampの低い側の辺
         * ・Rampの高い側の辺
         */

        // 底面が既存建築に接している
        if (CheckBuildConnection(
                position,
                new Vector3(
                    gridSize / 2f - 0.1f,
                    connectionThickness,
                    gridSize / 2f - 0.1f
                ),
                Quaternion.identity))
        {
            return true;
        }

        // 前側の面
        Vector3 frontCenter =
            position +
            forward * (gridSize / 2f) +
            Vector3.up * (gridSize / 2f);

        if (CheckBuildConnection(
                frontCenter,
                new Vector3(
                    gridSize / 2f - 0.1f,
                    gridSize / 2f - 0.1f,
                    connectionThickness
                ),
                rotation))
        {
            return true;
        }

        // 後ろ側の面
        Vector3 backCenter =
            position -
            forward * (gridSize / 2f) +
            Vector3.up * (gridSize / 2f);

        if (CheckBuildConnection(
                backCenter,
                new Vector3(
                    gridSize / 2f - 0.1f,
                    gridSize / 2f - 0.1f,
                    connectionThickness
                ),
                rotation))
        {
            return true;
        }

        // 右側の面
        Vector3 rightCenter =
            position +
            right * (gridSize / 2f) +
            Vector3.up * (gridSize / 2f);

        if (CheckBuildConnection(
                rightCenter,
                new Vector3(
                    connectionThickness,
                    gridSize / 2f - 0.1f,
                    gridSize / 2f - 0.1f
                ),
                rotation))
        {
            return true;
        }

        // 左側の面
        Vector3 leftCenter =
            position -
            right * (gridSize / 2f) +
            Vector3.up * (gridSize / 2f);

        if (CheckBuildConnection(
                leftCenter,
                new Vector3(
                    connectionThickness,
                    gridSize / 2f - 0.1f,
                    gridSize / 2f - 0.1f
                ),
                rotation))
        {
            return true;
        }

        /*
         * Rampを連続して上に伸ばす場合、
         * 前のRampの上端と、
         * 新しいRampの下端が線でつながる。
         */

        // Rampの低い側の辺
        Vector3 lowEdgeCenter =
            position -
            forward * (gridSize / 2f);

        if (CheckBuildConnection(
                lowEdgeCenter,
                new Vector3(
                    gridSize / 2f - 0.1f,
                    connectionThickness,
                    connectionThickness
                ),
                rotation))
        {
            return true;
        }

        // Rampの高い側の辺
        Vector3 highEdgeCenter =
            position +
            forward * (gridSize / 2f) +
            Vector3.up * gridSize;

        if (CheckBuildConnection(
                highEdgeCenter,
                new Vector3(
                    gridSize / 2f - 0.1f,
                    connectionThickness,
                    connectionThickness
                ),
                rotation))
        {
            return true;
        }

        // 地面にも建築にも接続していない
        return false;
    }

    /// <summary>
    /// Rampの底面が地面に触れているか確認する
    /// </summary>
    private bool IsConnectedToGround(Vector3 position)
    {
        Vector3 center =
            position +
            Vector3.up * connectionThickness;

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.1f,
            connectionThickness,
            gridSize / 2f - 0.1f
        );

        return Physics.CheckBox(
            center,
            halfExtents,
            Quaternion.identity,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    /// <summary>
    /// 指定した辺・面にBuild Layerの建築物が
    /// 接しているか確認する
    /// </summary>
    private bool CheckBuildConnection(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation
    )
    {
        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rotation,
            buildLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            // Preview自身は接続元にしない
            if (IsPreviewCollider(hit))
            {
                continue;
            }

            // Playerは接続元にしない
            if (IsPlayerCollider(hit))
            {
                continue;
            }

            // Build Layerの何かに触れている
            return true;
        }

        return false;
    }

    /// <summary>
    /// 候補位置がPlayerより後ろか確認する
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

        // 同じマスなら後ろ扱いにしない
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
    /// PlayerがRampに埋まるか確認する
    /// ジャンプして足元が上なら建築可能
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

        if (!sameXZ)
        {
            return false;
        }

        float playerBottomY =
            playerCollider.bounds.min.y;

        float blockedHeight =
            rampPosition.y +
            playerBlockHeight;

        return playerBottomY <= blockedHeight;
    }

    /// <summary>
    /// 候補Rampの内部に既存建築が重なっているか確認する
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
        // 辺だけ接している建築を誤検出しない
        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            gridSize / 2f - 0.15f,
            gridSize / 2f - 0.15f
        );

        Collider[] hits = Physics.OverlapBox(
            center,
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

            if (IsPlayerCollider(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Player自身またはPlayerの子Colliderか確認する
    /// </summary>
    private bool IsPlayerCollider(Collider hit)
    {
        return
            hit.transform == player ||
            hit.transform.IsChildOf(player);
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
    /// 建築可能・不可能でプレビューの色を変更する
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
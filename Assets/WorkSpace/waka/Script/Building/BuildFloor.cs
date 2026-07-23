using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 床建築を担当するクラス
///
/// ・床をグリッド中心へ配置する
/// ・下を向くとPlayerの真下へ候補を出す
/// ・正面ではPlayerの前の隣接マスへ候補を出す
/// ・階段の上では階段の先へ候補を出す
/// ・地面または既存建築につながる場合だけ建築可能
/// ・何にもつながらない空中建築は禁止
/// ・同じ場所への重複建築は禁止
/// ・建築不可能な場所は赤いプレビュー
/// </summary>
public class BuildFloor : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Tooltip("Player本体を設定する")]
    public Transform player;

    [Header("床Prefab")]
    public GameObject floorPrefab;
    public GameObject floorPreviewPrefab;

    [Header("プレビュー色")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("判定するLayer")]
    [Tooltip("地面に設定しているLayer")]
    public LayerMask groundLayer;

    [Tooltip("Wall・Ramp・Floorなど、すべての建築Layer")]
    public LayerMask buildLayer;

    [Tooltip("完成した床だけに設定しているLayer")]
    public LayerMask floorLayer;

    [Header("基本設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Tooltip("床Prefabの厚さ。床のScale Yと同じ値にする")]
    public float floorThickness = 0.1f;

    [Header("真下判定")]
    [Tooltip("この値よりカメラが下向きならPlayerの真下へ候補を出す")]
    [Range(-1f, 0f)]
    public float lookDownThreshold = -0.45f;

    [Header("接続判定")]
    [Tooltip("既存建築との接続判定に使う厚さ")]
    public float connectionThickness = 0.15f;

    private GameObject currentPreview;

    // このスクリプトから建築した床の位置を記録する
    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// 床プレビューを生成する
    /// </summary>
    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        if (floorPreviewPrefab == null)
        {
            Debug.LogError(
                "BuildFloorのFloor Preview Prefabが未設定です"
            );

            return;
        }

        currentPreview =
            Instantiate(floorPreviewPrefab);

        currentPreview.SetActive(true);
    }

    /// <summary>
    /// 床プレビューを削除する
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
    /// 床プレビューの位置・向き・色を更新する
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

        // 何にもつながらない空中なら候補を消す
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

        SetPreviewMaterial(canBuild);
    }

    /// <summary>
    /// 床を実際に建築する
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

        string key =
            GetBuildKey(position);

        // 同じ場所には建築しない
        if (builtPositions.Contains(key))
        {
            return;
        }

        if (floorPrefab == null)
        {
            Debug.LogError(
                "BuildFloorのFloor Prefabが未設定です"
            );

            return;
        }

        Instantiate(
            floorPrefab,
            position,
            rotation
        );

        builtPositions.Add(key);
    }

    /// <summary>
    /// 床の候補位置と建築可能状態を取得する
    ///
    /// 戻り値：
    /// true  = 候補を表示する
    /// false = 候補自体を表示しない
    ///
    /// canBuild：
    /// true  = 建築可能
    /// false = 赤いプレビュー
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild
    )
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        canBuild = true;

        if (playerCamera == null || player == null)
        {
            return false;
        }

        Vector3 cameraForward =
            playerCamera.transform.forward;

        Vector3 buildPoint;

        /*
         * カメラを下へ向けている場合は
         * Rayの当たり位置ではなくPlayer自身のマスを使用する。
         */
        bool lookingDown =
            cameraForward.y <
            lookDownThreshold;

        if (lookingDown)
        {
            buildPoint =
                new Vector3(
                    player.position.x,
                    GetPlayerFloorBuildLevel(),
                    player.position.z
                );
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
                /*
                 * 地面や建築物にRayが当たった場合は、
                 * 当たった面の少し外側を候補にする。
                 */
                buildPoint =
                    hit.point +
                    hit.normal * 0.1f;
            }
            else
            {
                /*
                 * Rayが当たらなければ、
                 * Playerの前の隣接グリッドへ候補を出す。
                 */
                Vector3 forward =
                    GetSnappedPlayerForward();

                if (forward.sqrMagnitude <= 0.001f)
                {
                    return false;
                }

                Vector3 playerGrid =
                    new Vector3(
                        Mathf.Round(
                            player.position.x /
                            gridSize
                        ) * gridSize,

                        GetPlayerFloorBuildLevel(),

                        Mathf.Round(
                            player.position.z /
                            gridSize
                        ) * gridSize
                    );

                /*
                 * buildDistanceではなくgridSizeを使用する。
                 * これで階段の先の次の1マスに配置される。
                 */
                buildPoint =
                    playerGrid +
                    forward * gridSize;
            }
        }

        // 床をグリッド位置へ変換
        position =
            GetFloorGridPosition(buildPoint);

        /*
         * 地面または既存建築につながっていなければ
         * 空中建築になるため候補を表示しない。
         */
        if (!IsFloorConnected(position))
        {
            return false;
        }

        /*
         * Playerと同じマスなら後ろ判定をしない。
         * これによってPlayerの真下へ建築できる。
         */
        if (!IsSamePlayerGrid(position) &&
            IsBehindPlayer(position))
        {
            canBuild = false;
        }

        string key =
            GetBuildKey(position);

        // このスクリプトから同じ場所に建築済み
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // シーン内に完成済みの床がある
        if (IsOverlappingFloor(position))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// Playerが向いている方向を前後左右の4方向へ丸める
    /// </summary>
    private Vector3 GetSnappedPlayerForward()
    {
        Vector3 forward =
            player.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
        {
            forward =
                playerCamera.transform.forward;

            forward.y = 0f;
        }

        if (forward.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        forward.Normalize();

        if (Mathf.Abs(forward.x) >
            Mathf.Abs(forward.z))
        {
            return new Vector3(
                Mathf.Sign(forward.x),
                0f,
                0f
            );
        }

        return new Vector3(
            0f,
            0f,
            Mathf.Sign(forward.z)
        );
    }

    /// <summary>
    /// 床をグリッド位置へ配置する
    /// </summary>
    private Vector3 GetFloorGridPosition(
        Vector3 buildPoint
    )
    {
        Vector3 position =
            buildPoint;

        // XとZはグリッド中心へ吸着
        position.x =
            Mathf.Round(position.x / gridSize) *
            gridSize;

        position.z =
            Mathf.Round(position.z / gridSize) *
            gridSize;

        // 床を配置する高さを0、4、8...へ合わせる
        float floorLevel =
            Mathf.Round(position.y / gridSize) *
            gridSize;

        /*
         * 床PrefabのPivotが中央にあるため、
         * 実際の厚さの半分だけ上へ持ち上げる。
         */
        position.y =
            floorLevel +
            GetActualFloorThickness() / 2f;

        return position;
    }

    /// <summary>
    /// Playerの足元から床を置く階層を取得する
    ///
    /// 階段の下半分では下の階層、
    /// 階段の上半分まで登ると上の階層になる。
    /// </summary>
    private float GetPlayerFloorBuildLevel()
    {
        float playerBottomY =
            player.position.y;

        Collider playerCollider =
            player.GetComponent<Collider>();

        if (playerCollider != null)
        {
            playerBottomY =
                playerCollider.bounds.min.y;
        }

        return Mathf.Round(
            playerBottomY / gridSize
        ) * gridSize;
    }

    /// <summary>
    /// 床PrefabのColliderから実際の厚さを取得する
    /// Colliderがない場合はFloor Thicknessを使用する
    /// </summary>
    private float GetActualFloorThickness()
    {
        if (floorPrefab == null)
        {
            return floorThickness;
        }

        BoxCollider floorCollider =
            floorPrefab.GetComponentInChildren<BoxCollider>();

        if (floorCollider == null)
        {
            return floorThickness;
        }

        float thickness =
            floorCollider.size.y *
            Mathf.Abs(
                floorCollider.transform.lossyScale.y
            );

        if (thickness <= 0.001f)
        {
            return floorThickness;
        }

        return thickness;
    }

    /// <summary>
    /// 候補がPlayerと同じXZグリッドか確認する
    /// </summary>
    private bool IsSamePlayerGrid(
        Vector3 floorPosition
    )
    {
        float playerGridX =
            Mathf.Round(
                player.position.x / gridSize
            ) * gridSize;

        float playerGridZ =
            Mathf.Round(
                player.position.z / gridSize
            ) * gridSize;

        return
            Mathf.Approximately(
                floorPosition.x,
                playerGridX
            ) &&
            Mathf.Approximately(
                floorPosition.z,
                playerGridZ
            );
    }

    /// <summary>
    /// 床が地面または既存建築につながっているか確認する
    /// </summary>
    private bool IsFloorConnected(
        Vector3 floorPosition
    )
    {
        // 地面に接している
        if (IsConnectedToGround(floorPosition))
        {
            return true;
        }

        // 床の真下に建築物がある
        if (HasBuildBelow(floorPosition))
        {
            return true;
        }

        // 床の前後左右の辺に建築物がある
        if (HasBuildBeside(floorPosition))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 床の下に地面があるか確認する
    /// </summary>
    private bool IsConnectedToGround(
        Vector3 floorPosition
    )
    {
        float actualThickness =
            GetActualFloorThickness();

        float floorLevel =
            floorPosition.y -
            actualThickness / 2f;

        // 地面階以外では地面判定を行わない
        if (floorLevel > 0.1f)
        {
            return false;
        }

        /*
         * 床のすぐ下をBoxで確認する。
         * 四隅Rayより安定して地面を検出できる。
         */
        Vector3 checkCenter =
            floorPosition -
            Vector3.up *
            (
                actualThickness / 2f +
                0.1f
            );

        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.15f,
                0.15f,
                gridSize / 2f - 0.15f
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
    /// 床の真下に建築物があるか確認する
    /// </summary>
    private bool HasBuildBelow(
        Vector3 floorPosition
    )
    {
        float actualThickness =
            GetActualFloorThickness();

        Vector3 checkCenter =
            floorPosition -
            Vector3.up *
            (
                actualThickness / 2f +
                connectionThickness
            );

        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.15f,
                connectionThickness,
                gridSize / 2f - 0.15f
            );

        Collider[] hits =
            Physics.OverlapBox(
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

            return true;
        }

        return false;
    }

    /// <summary>
    /// 床の前後左右の辺に建築物があるか確認する
    /// </summary>
    private bool HasBuildBeside(
        Vector3 floorPosition
    )
    {
        float halfGrid =
            gridSize / 2f;

        Vector3[] checkCenters =
        {
            floorPosition +
            Vector3.forward * halfGrid,

            floorPosition +
            Vector3.back * halfGrid,

            floorPosition +
            Vector3.right * halfGrid,

            floorPosition +
            Vector3.left * halfGrid
        };

        for (int i = 0;
             i < checkCenters.Length;
             i++)
        {
            Vector3 halfExtents;

            // 前後の辺
            if (i < 2)
            {
                halfExtents =
                    new Vector3(
                        halfGrid - 0.15f,
                        connectionThickness,
                        connectionThickness
                    );
            }
            // 左右の辺
            else
            {
                halfExtents =
                    new Vector3(
                        connectionThickness,
                        connectionThickness,
                        halfGrid - 0.15f
                    );
            }

            Collider[] hits =
                Physics.OverlapBox(
                    checkCenters[i],
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

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 同じ場所に完成済みの床があるか確認する
    /// </summary>
    private bool IsOverlappingFloor(
        Vector3 position
    )
    {
        float actualThickness =
            GetActualFloorThickness();

        Vector3 halfExtents =
            new Vector3(
                gridSize / 2f - 0.1f,
                actualThickness / 2f,
                gridSize / 2f - 0.1f
            );

        Collider[] hits =
            Physics.OverlapBox(
                position,
                halfExtents,
                Quaternion.identity,
                floorLayer,
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
    /// 床候補がPlayerより後ろにあるか確認する
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
    /// 建築可能状態に応じてプレビュー色を変更する
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
    /// 建築済み判定用のキーを作る
    /// </summary>
    private string GetBuildKey(
        Vector3 position
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

        return
            x + "_" +
            y + "_" +
            z;
    }
}
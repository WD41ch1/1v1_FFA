using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コーン建築を担当する独立クラス
///
/// ・床と同じグリッドへ配置
/// ・床に照準が当たった場合は、その床の中央へ配置
/// ・床の上へ重ねて建築可能
/// ・Cone同士の重複は禁止
/// ・Prefabの向きをY回転で補正可能
/// </summary>
public class BuildCone : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    [Tooltip("Player本体")]
    public Transform player;

    [Header("Prefab")]
    public GameObject conePrefab;
    public GameObject conePreviewPrefab;

    [Header("プレビューMaterial")]
    public Material canBuildMaterial;
    public Material cannotBuildMaterial;

    [Header("Layer")]
    [Tooltip("地面のLayer")]
    public LayerMask groundLayer;

    [Tooltip("Wall・Ramp・Floor・Coneなどの建築Layer")]
    public LayerMask buildLayer;

    [Tooltip("完成した床だけのLayer")]
    public LayerMask floorLayer;

    [Tooltip("完成したConeだけのLayer")]
    public LayerMask coneLayer;

    [Header("グリッド設定")]
    [Tooltip("BuildFloorと同じ値にする")]
    public float gridSize = 4f;

    public float buildDistance = 6f;

    [Header("Cone設定")]
    [Tooltip("Cone底面からPrefabのPivotまでの高さ")]
    public float conePivotFromBottom = 0f;

    [Tooltip("Cone全体の高さ")]
    public float coneHeight = 2f;

    [Tooltip("床と向きを合わせる回転。画像のConeなら45")]
    public float coneRotationY = 45f;

    [Header("視線設定")]
    [Range(-1f, 0f)]
    public float lookDownThreshold = -0.45f;

    [Header("接続判定")]
    public float connectionThickness = 0.2f;

    private GameObject currentPreview;

    private readonly HashSet<string> builtPositions =
        new HashSet<string>();

    /// <summary>
    /// プレビューを生成
    /// </summary>
    public void ShowPreview()
    {
        HidePreview();

        if (conePreviewPrefab == null)
        {
            Debug.LogError(
                "BuildCone: Cone Preview Prefabが未設定です"
            );
            return;
        }

        currentPreview =
            Instantiate(conePreviewPrefab);

        currentPreview.SetActive(true);
    }

    /// <summary>
    /// プレビューを削除
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
    /// プレビューを更新
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

        currentPreview.SetActive(true);

        currentPreview.transform.SetPositionAndRotation(
            position,
            rotation
        );

        SetPreviewMaterial(canBuild);
    }

    /// <summary>
    /// Coneを建築
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

        if (conePrefab == null)
        {
            Debug.LogError(
                "BuildCone: Cone Prefabが未設定です"
            );
            return;
        }

        string key = GetBuildKey(position);

        if (builtPositions.Contains(key))
        {
            return;
        }

        Instantiate(
            conePrefab,
            position,
            rotation
        );

        builtPositions.Add(key);
    }

    /// <summary>
    /// 建築候補を取得
    /// </summary>
    private bool TryGetBuildPoint(
        out Vector3 position,
        out Quaternion rotation,
        out bool canBuild
    )
    {
        position = Vector3.zero;

        rotation = Quaternion.Euler(
            0f,
            coneRotationY,
            0f
        );

        canBuild = false;

        if (playerCamera == null || player == null)
        {
            return false;
        }

        Vector3 cameraForward =
            playerCamera.transform.forward;

        Vector3 buildPoint;

        bool lookingDown =
            cameraForward.y <
            lookDownThreshold;

        if (lookingDown)
        {
            // Playerと同じグリッド
            buildPoint = new Vector3(
                player.position.x,
                GetPlayerBuildLevel(),
                player.position.z
            );
        }
        else
        {
            int rayMask =
                groundLayer.value |
                buildLayer.value |
                floorLayer.value |
                coneLayer.value;

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
                if (IsLayerInMask(
                    hit.collider.gameObject.layer,
                    floorLayer))
                {
                    /*
                     * 床に当たった場合は命中位置ではなく、
                     * 床Colliderの中央を使用する。
                     */
                    buildPoint = new Vector3(
                        hit.collider.bounds.center.x,
                        hit.collider.bounds.max.y,
                        hit.collider.bounds.center.z
                    );
                }
                else
                {
                    buildPoint =
                        hit.point +
                        hit.normal * 0.1f;
                }
            }
            else
            {
                Vector3 forward =
                    GetSnappedPlayerForward();

                if (forward.sqrMagnitude <= 0.001f)
                {
                    return false;
                }

                Vector3 playerGrid = new Vector3(
                    Mathf.Round(
                        player.position.x / gridSize
                    ) * gridSize,

                    GetPlayerBuildLevel(),

                    Mathf.Round(
                        player.position.z / gridSize
                    ) * gridSize
                );

                buildPoint =
                    playerGrid +
                    forward * gridSize;
            }
        }

        position =
            GetConeGridPosition(buildPoint);

        /*
         * 地面・床・既存建築のどれにも
         * 接続していない場合は候補を非表示。
         */
        if (!IsConnected(position))
        {
            return false;
        }

        canBuild = true;

        // Playerより後ろのマスは建築不可
        if (!IsSamePlayerGrid(position) &&
            IsBehindPlayer(position))
        {
            canBuild = false;
        }

        string key = GetBuildKey(position);

        // このスクリプトが配置したConeと重複
        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // シーン内の完成済みConeと重複
        if (IsOverlappingCone(position))
        {
            canBuild = false;
        }

        return true;
    }

    /// <summary>
    /// 床と同じグリッドへ変換
    /// </summary>
    private Vector3 GetConeGridPosition(
        Vector3 buildPoint
    )
    {
        float gridX =
            Mathf.Round(buildPoint.x / gridSize) *
            gridSize;

        float gridZ =
            Mathf.Round(buildPoint.z / gridSize) *
            gridSize;

        float buildLevel =
            Mathf.Round(buildPoint.y / gridSize) *
            gridSize;

        /*
         * Coneの底面を0、4、8...へ合わせる。
         * Transform位置はPivot分だけ上へ移動する。
         */
        float pivotY =
            buildLevel +
            conePivotFromBottom;

        return new Vector3(
            gridX,
            pivotY,
            gridZ
        );
    }

    /// <summary>
    /// Playerの足元に近い建築階層を取得
    /// </summary>
    private float GetPlayerBuildLevel()
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
    /// Playerの向きを前後左右へ丸める
    /// </summary>
    private Vector3 GetSnappedPlayerForward()
    {
        Vector3 forward = player.forward;
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
    /// 地面・床・既存建築への接続判定
    /// </summary>
    private bool IsConnected(Vector3 conePosition)
    {
        float coneBaseY =
            conePosition.y -
            conePivotFromBottom;

        Vector3 checkCenter = new Vector3(
            conePosition.x,
            coneBaseY - connectionThickness,
            conePosition.z
        );

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.15f,
            connectionThickness,
            gridSize / 2f - 0.15f
        );

        int connectionMask =
            groundLayer.value |
            buildLayer.value |
            floorLayer.value;

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            connectionMask,
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

        return IsConnectedBeside(conePosition);
    }

    /// <summary>
    /// 前後左右に建築物があるか確認
    /// </summary>
    private bool IsConnectedBeside(
        Vector3 conePosition
    )
    {
        float coneBaseY =
            conePosition.y -
            conePivotFromBottom;

        float halfGrid =
            gridSize / 2f;

        Vector3 basePosition = new Vector3(
            conePosition.x,
            coneBaseY,
            conePosition.z
        );

        Vector3[] centers =
        {
            basePosition + Vector3.forward * halfGrid,
            basePosition + Vector3.back * halfGrid,
            basePosition + Vector3.right * halfGrid,
            basePosition + Vector3.left * halfGrid
        };

        int connectionMask =
            buildLayer.value |
            floorLayer.value;

        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 halfExtents;

            if (i < 2)
            {
                halfExtents = new Vector3(
                    halfGrid - 0.15f,
                    connectionThickness,
                    connectionThickness
                );
            }
            else
            {
                halfExtents = new Vector3(
                    connectionThickness,
                    connectionThickness,
                    halfGrid - 0.15f
                );
            }

            Collider[] hits = Physics.OverlapBox(
                centers[i],
                halfExtents,
                Quaternion.identity,
                connectionMask,
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
    /// 完成済みConeとの重複判定
    /// 床は重複判定へ含めない
    /// </summary>
    private bool IsOverlappingCone(
        Vector3 position
    )
    {
        float coneBaseY =
            position.y -
            conePivotFromBottom;

        Vector3 checkCenter = new Vector3(
            position.x,
            coneBaseY + coneHeight / 2f,
            position.z
        );

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.1f,
            Mathf.Max(
                coneHeight / 2f - 0.05f,
                0.05f
            ),
            gridSize / 2f - 0.1f
        );

        Collider[] hits = Physics.OverlapBox(
            checkCenter,
            halfExtents,
            Quaternion.identity,
            coneLayer,
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
    /// Playerと同じXZグリッドか確認
    /// </summary>
    private bool IsSamePlayerGrid(
        Vector3 position
    )
    {
        float playerGridX =
            Mathf.Round(player.position.x / gridSize) *
            gridSize;

        float playerGridZ =
            Mathf.Round(player.position.z / gridSize) *
            gridSize;

        return
            Mathf.Approximately(
                position.x,
                playerGridX
            ) &&
            Mathf.Approximately(
                position.z,
                playerGridZ
            );
    }

    /// <summary>
    /// 候補がPlayerの後ろか確認
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

        Vector3 toBuild =
            position -
            player.position;

        toBuild.y = 0f;

        if (toBuild.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        return Vector3.Dot(
            playerForward.normalized,
            toBuild.normalized
        ) < 0f;
    }

    /// <summary>
    /// プレビュー自身のColliderか確認
    /// </summary>
    private bool IsPreviewCollider(Collider hit)
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
    /// LayerMaskにLayerが含まれるか確認
    /// </summary>
    private bool IsLayerInMask(
        int layer,
        LayerMask layerMask
    )
    {
        return
            (layerMask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// プレビューのMaterialを変更
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
            currentPreview
                .GetComponentsInChildren<Renderer>();

        foreach (Renderer targetRenderer in renderers)
        {
            targetRenderer.material =
                targetMaterial;
        }
    }

    /// <summary>
    /// 建築済み位置のキーを生成
    /// </summary>
    private string GetBuildKey(Vector3 position)
    {
        /*
         * Pivotの位置ではなくCone底面の
         * グリッド階層をキーに使用する。
         */
        float baseY =
            position.y -
            conePivotFromBottom;

        int gridX =
            Mathf.RoundToInt(
                position.x / gridSize
            );

        int gridY =
            Mathf.RoundToInt(
                baseY / gridSize
            );

        int gridZ =
            Mathf.RoundToInt(
                position.z / gridSize
            );

        return
            gridX + "_" +
            gridY + "_" +
            gridZ;
    }
}
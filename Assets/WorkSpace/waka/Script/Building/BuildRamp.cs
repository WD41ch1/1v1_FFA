using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 階段建築を担当するクラス
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

    [Header("設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    [Header("Player重なり判定")]
    public float playerBlockHeight = 1.2f;

    private GameObject currentPreview;
    private HashSet<string> builtPositions = new HashSet<string>();

    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(rampPreviewPrefab);
    }

    public void HidePreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    public void UpdatePreview()
    {
        if (currentPreview == null) return;

        bool canBuild = GetBuildPoint(out Vector3 position, out Quaternion rotation);

        currentPreview.SetActive(true);
        currentPreview.transform.position = position;
        currentPreview.transform.rotation = rotation;

        SetPreviewMaterial(canBuild);
    }

    public void Build()
    {
        bool canBuild = GetBuildPoint(out Vector3 position, out Quaternion rotation);

        if (!canBuild)
        {
            return;
        }

        string key = GetBuildKey(position, rotation);

        if (builtPositions.Contains(key))
        {
            return;
        }

        Instantiate(rampPrefab, position, rotation);
        builtPositions.Add(key);
    }

    private bool GetBuildPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = GetRampRotation();

        // カメラ中央からRayを飛ばす
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Vector3 buildPoint;

        // Rayが当たった場所を建築候補にする
        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            buildPoint = hit.point;
        }
        else
        {
            // Rayが当たらなかったら、カメラの水平前方向に候補を出す
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            buildPoint = playerCamera.transform.position + forward * buildDistance;
            buildPoint.y = 0f;
        }

        // Rampは1マスの中心に置く
        position = GetRampGridPosition(buildPoint);

        bool canBuild = true;

        // Playerより後ろには建築できない
        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        Vector3 toBuild = position - player.position;
        toBuild.y = 0f;

        // 同じマスのときは後ろ判定しない
        if (toBuild.sqrMagnitude > 0.01f)
        {
            if (Vector3.Dot(playerForward, toBuild.normalized) < 0f)
            {
                canBuild = false;
            }
        }

        // PlayerがRampに埋まる高さなら建築できない
        // ジャンプして足元が十分上なら建築できる
        if (IsPlayerOverlappingRamp(position, rotation))
        {
            canBuild = false;
        }

        // すでに同じ位置・同じ向きにRampがあるなら建築できない
        string key = GetBuildKey(position, rotation);

        if (builtPositions.Contains(key))
        {
            canBuild = false;
        }

        // 壁やRampなど、既存建築と物理的に重なるなら建築できない
        if (IsOverlappingBuild(position, rotation))
        {
            canBuild = false;
        }

        return canBuild;
    }

    private Vector3 GetRampGridPosition(Vector3 buildPoint)
    {
        Vector3 pos = SnapToGrid(buildPoint);

        // RampPrefabのPivotが地面にある前提
        pos.y = 0f;

        return pos;
    }

    private bool IsPlayerOverlappingRamp(Vector3 rampPosition, Quaternion rampRotation)
    {
        Collider playerCollider = player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;

        // PlayerとRampが同じXZマスか見る
        Vector3 playerGrid = SnapToGrid(player.position);

        bool sameXZ =
            rampPosition.x == playerGrid.x &&
            rampPosition.z == playerGrid.z;

        // 同じマスじゃないならPlayerとは被らない扱い
        if (!sameXZ)
        {
            return false;
        }

        // Playerの足元の高さ
        float playerBottomY = playerBounds.min.y;

        // この高さより足元が上なら、ジャンプで避けている扱い
        float blockY = rampPosition.y + playerBlockHeight;

        if (playerBottomY > blockY)
        {
            return false;
        }

        // 同じマスで足元が低いなら、Rampに埋まるので建築不可
        return true;
    }

    private bool IsOverlappingBuild(Vector3 position, Quaternion rotation)
    {
        // Rampが入る1マス分の箱でチェック
        Vector3 center = position + Vector3.up * (gridSize / 2f);

        Vector3 halfExtents = new Vector3(
            gridSize / 2f - 0.05f,
            gridSize / 2f - 0.05f,
            gridSize / 2f - 0.05f
        );

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rotation
        );

        foreach (Collider hit in hits)
        {
            // Preview自身は無視
            if (currentPreview != null && hit.transform.IsChildOf(currentPreview.transform))
            {
                continue;
            }

            // Playerは別の判定で見るので無視
            if (hit.transform == player || hit.transform.IsChildOf(player))
            {
                continue;
            }

            // PreviewとPlayer以外に当たったら既存建築と重なっている扱い
            return true;
        }

        return false;
    }

    private void SetPreviewMaterial(bool canBuild)
    {
        if (currentPreview == null) return;

        Material targetMaterial = canBuild ? canBuildMaterial : cannotBuildMaterial;

        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            renderer.material = targetMaterial;
        }
    }

    private Vector3 SnapToGrid(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.y = Mathf.Round(pos.y / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;

        return pos;
    }

    private Quaternion GetRampRotation()
    {
        float cameraY = playerCamera.transform.eulerAngles.y;
        float snappedY = Mathf.Round(cameraY / 90f) * 90f;

        return Quaternion.Euler(0f, snappedY, 0f);
    }

    private string GetBuildKey(Vector3 position, Quaternion rotation)
    {
        int x = Mathf.RoundToInt(position.x * 100f);
        int y = Mathf.RoundToInt(position.y * 100f);
        int z = Mathf.RoundToInt(position.z * 100f);
        int rotY = Mathf.RoundToInt(rotation.eulerAngles.y);

        return x + "_" + y + "_" + z + "_" + rotY;
    }
}
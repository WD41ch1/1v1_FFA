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

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Vector3 buildPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            // 建築物や地面に当たったら、当たった面の少し外側を基準にする
            buildPoint = hit.point + hit.normal * 0.1f;
        }
        else
        {
            // 空を向いていてRayが当たらない時でも、
            // Playerの前のグリッドに次のRamp候補を出す
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward.Normalize();

            buildPoint = player.position + forward * gridSize;

            // 今いる高さをグリッドに合わせる
            buildPoint.y = Mathf.Round(player.position.y / gridSize) * gridSize;
        }

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

        // Yもグリッドに合わせるので、上方向にも繋げられる
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

        Vector3 playerGrid = SnapToGrid(player.position);

        bool sameXZ =
            rampPosition.x == playerGrid.x &&
            rampPosition.z == playerGrid.z;

        if (!sameXZ)
        {
            return false;
        }

        float playerBottomY = playerBounds.min.y;
        float blockY = rampPosition.y + playerBlockHeight;

        if (playerBottomY > blockY)
        {
            return false;
        }

        return true;
    }

    private bool IsOverlappingBuild(Vector3 position, Quaternion rotation)
    {
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
            if (currentPreview != null && hit.transform.IsChildOf(currentPreview.transform))
            {
                continue;
            }

            if (hit.transform == player || hit.transform.IsChildOf(player))
            {
                continue;
            }

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
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁建築を担当するクラス
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

    [Header("設定")]
    public float buildDistance = 6f;
    public float gridSize = 4f;

    private GameObject currentPreview;
    private HashSet<string> builtPositions = new HashSet<string>();

    public void ShowPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(wallPreviewPrefab);
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

        string key = GetBuildKey(position, rotation);

        // すでに建っている場所ならプレビューを消す
        if (builtPositions.Contains(key))
        {
            currentPreview.SetActive(false);
            return;
        }

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

        Instantiate(wallPrefab, position, rotation);
        builtPositions.Add(key);
    }

    private bool GetBuildPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = GetWallRotation();

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Vector3 buildPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            buildPoint = hit.point;
        }
        else
        {
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            buildPoint =
                playerCamera.transform.position +
                forward * buildDistance;

            buildPoint.y = 0f;
        }

        position = GetWallGridPosition(buildPoint, rotation);

        bool canBuild = true;

        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 toBuildPoint = buildPoint - playerCamera.transform.position;
        toBuildPoint.y = 0f;

        if (Vector3.Dot(cameraForward, toBuildPoint) < 0f)
        {
            canBuild = false;
        }

        string key = GetBuildKey(position, rotation);

        

        return canBuild;
    }

    private Vector3 GetWallGridPosition(Vector3 buildPoint, Quaternion rotation)
    {
        Vector3 pos = SnapToGrid(buildPoint);

        pos.y += gridSize / 2f;

        float y = rotation.eulerAngles.y;
        y = Mathf.Round(y / 90f) * 90f;

        if (y >= 360f)
        {
            y = 0f;
        }

        if (y == 0f)
        {
            pos.z += gridSize / 2f;
        }
        else if (y == 90f)
        {
            pos.x += gridSize / 2f;
        }
        else if (y == 180f)
        {
            pos.z -= gridSize / 2f;
        }
        else if (y == 270f)
        {
            pos.x -= gridSize / 2f;
        }

        return pos;
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

    private Quaternion GetWallRotation()
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
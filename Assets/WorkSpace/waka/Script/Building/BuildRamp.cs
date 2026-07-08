using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 階段建築を担当するクラス
/// </summary>
public class BuildRamp : MonoBehaviour
{
    [Header("参照")]
    public Camera playerCamera;

    // Player本体を入れる
    public Transform player;

    [Header("階段Prefab")]
    public GameObject rampPrefab;
    public GameObject rampPreviewPrefab;

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

        if (GetBuildPoint(out Vector3 position, out Quaternion rotation))
        {
            currentPreview.SetActive(true);
            currentPreview.transform.position = position;
            currentPreview.transform.rotation = rotation;
        }
        else
        {
            currentPreview.SetActive(false);
        }
    }

    public void Build()
    {
        if (GetBuildPoint(out Vector3 position, out Quaternion rotation))
        {
            string key = GetBuildKey(position, rotation);

            if (builtPositions.Contains(key))
            {
                return;
            }

            Instantiate(rampPrefab, position, rotation);
            builtPositions.Add(key);
        }
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
            buildPoint = hit.point;
        }
        else
        {
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            buildPoint = playerCamera.transform.position + forward * buildDistance;
            buildPoint.y = 0f;
        }

        position = GetRampGridPosition(buildPoint);

        // Playerより後ろには建築できない
        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        Vector3 toBuild = position - player.position;
        toBuild.y = 0f;

        if (Vector3.Dot(playerForward, toBuild.normalized) < 0f)
        {
            return false;
        }

        // PlayerがRampの空間に埋まるなら建築禁止
        if (IsPlayerOverlappingRamp(position, rotation))
        {
            return false;
        }

        return true;
    }

    private Vector3 GetRampGridPosition(Vector3 buildPoint)
    {
        Vector3 pos = SnapToGrid(buildPoint);

        // RampPrefabのPivotが地面にある前提
        pos.y = 0f;

        return pos;
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
    private bool IsPlayerOverlappingRamp(Vector3 rampPosition, Quaternion rampRotation)
    {
        Collider playerCollider = player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;

        Vector3 rampCenter = rampPosition + Vector3.up * (gridSize / 2f);

        Vector3 rampSize = new Vector3(
            gridSize,
            gridSize,
            gridSize
        );

        Bounds rampBounds = new Bounds(rampCenter, rampSize);

        return rampBounds.Intersects(playerBounds);
    }

}
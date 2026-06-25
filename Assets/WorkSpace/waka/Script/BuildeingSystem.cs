using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    public enum BuildType
    {
        None,
        Wall
    }

    [Header("参照")]
    public Camera playerCamera;

    [Header("壁Prefab")]
    public GameObject wallPrefab;
    public GameObject wallPreviewPrefab;

    [Header("設定")]
    public float buildDistance = 10f;
    public float gridSize = 4f;

    private BuildType currentBuildType = BuildType.None;
    private GameObject currentPreview;

    private void Update()
    {
        // マウスサイドボタンで壁モード
        if (Input.GetMouseButtonDown(3))
        {
            SelectWall();
        }

        // 壁モード中だけプレビュー更新
        if (currentBuildType == BuildType.Wall)
        {
            UpdatePreview();

            // 左クリックで確定
            if (Input.GetMouseButtonDown(0))
            {
                BuildWall();
            }
        }
    }

    private void SelectWall()
    {
        currentBuildType = BuildType.Wall;

        // すでにプレビューがあるなら消す
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        // 壁の仮表示を作る
        currentPreview = Instantiate(wallPreviewPrefab);
    }

    private void UpdatePreview()
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

    private void BuildWall()
    {
        if (GetBuildPoint(out Vector3 position, out Quaternion rotation))
        {
            Instantiate(wallPrefab, position, rotation);
        }
    }

    private bool GetBuildPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            position = SnapToGrid(hit.point);

            // 壁は中心を上にずらす
            position.y += gridSize / 2f;

            rotation = GetWallRotation();

            return true;
        }

        return false;
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

        // 90度単位で向きを固定
        float snappedY = Mathf.Round(cameraY / 90f) * 90f;

        return Quaternion.Euler(0f, snappedY, 0f);
    }
}
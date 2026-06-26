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

            Instantiate(wallPrefab, position, rotation);
            builtPositions.Add(key);
        }
    }

    private bool GetBuildPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;

        // カメラの向きから壁の向きを決める
        rotation = GetWallRotation();

        // カメラ中央からRayを飛ばす
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        Vector3 buildPoint;

        // Rayが地面などに当たった場合
        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            // 当たった場所を建築基準にする
            buildPoint = hit.point;
        }
        else
        {
            // Rayが当たらなかった場合は、
            // カメラの水平前方向へ決めた距離進める
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();

            buildPoint =
                playerCamera.transform.position +
                forward * buildDistance;

            // 地面の高さに落とす
            buildPoint.y = 0f;
        }

        // プレイヤーの後ろに出ないようにする
        Vector3 cameraForward = playerCamera.transform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 toBuildPoint =
            buildPoint - playerCamera.transform.position;

        toBuildPoint.y = 0f;

        // カメラ前方向との内積がマイナスなら後ろ
        if (Vector3.Dot(cameraForward, toBuildPoint) < 0f)
        {
            return false;
        }

        // 壁専用のグリッドの淵に置く
        position = GetWallGridPosition(buildPoint, rotation);

        return true;
    }

    private Vector3 GetWallGridPosition(Vector3 buildPoint, Quaternion rotation)
    {
        // まず建築基準位置をグリッドの中心に吸着させる
        Vector3 pos = SnapToGrid(buildPoint);

        // 壁PrefabのPivotが中心にある前提なので、
        // 壁の高さの半分だけ上に上げて地面に埋まらないようにする
        pos.y += gridSize / 2f;

        // 壁のY回転を0〜360の範囲にする
        float y = rotation.eulerAngles.y;

        // 90度単位に丸める
        y = Mathf.Round(y / 90f) * 90f;

        // 360度は0度として扱う
        if (y >= 360f)
        {
            y = 0f;
        }

        // 壁の向きに合わせて、
        // グリッド中心から「前側の淵」にずらす
        if (y == 0f)
        {
            // +Z方向
            pos.z += gridSize / 2f;
        }
        else if (y == 90f)
        {
            // +X方向
            pos.x += gridSize / 2f;
        }
        else if (y == 180f)
        {
            // -Z方向
            pos.z -= gridSize / 2f;
        }
        else if (y == 270f)
        {
            // -X方向
            pos.x -= gridSize / 2f;
        }

        return pos;
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
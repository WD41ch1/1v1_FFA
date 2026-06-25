using System.Collections.Generic;
using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    public enum BuildType
    {
        None,
        Wall
    }

    [Header("参照")]
    public PlayerInputController input; // 入力管理
    public Camera playerCamera;         // プレイヤーのカメラ

    [Header("壁Prefab")]
    public GameObject wallPrefab;        // 本物の壁
    public GameObject wallPreviewPrefab; // 仮表示の壁

    [Header("設定")]
    public float buildDistance = 6f; // 建築可能距離
    public float gridSize = 4f;      // グリッドサイズ

    private BuildType currentBuildType = BuildType.None;
    private GameObject currentPreview;

    // すでに建築した場所を記録する
    private HashSet<string> builtPositions = new HashSet<string>();

    private void Update()
    {
        // サイドボタン手前側で壁モードにする
        if (input.BuildWallPressed)
        {
            SelectWall();
            input.ResetBuildWall();
        }

        // 壁モード中だけプレビューを動かす
        if (currentBuildType == BuildType.Wall)
        {
            UpdatePreview();

            // 左クリックで確定建築
            if (Input.GetMouseButtonDown(0))
            {
                BuildWall();
            }
        }
    }

    private void SelectWall()
    {
        currentBuildType = BuildType.Wall;

        // 古いプレビューがあれば消す
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
            string key = GetBuildKey(position, rotation);

            // 同じ場所・同じ向きには置かない
            if (builtPositions.Contains(key))
            {
                Debug.Log("ここにはすでに壁があります");
                return;
            }

            Instantiate(wallPrefab, position, rotation);

            // 建築した位置を保存
            builtPositions.Add(key);
        }
    }

    private bool GetBuildPoint(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        // カメラ中央からRayを飛ばす
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        // 壁の向きをカメラ方向から決める
        rotation = GetWallRotation();

        Vector3 buildPoint;

        // Rayが地面や壁に当たった場合
        if (Physics.Raycast(ray, out RaycastHit hit, buildDistance))
        {
            // 当たった場所をそのまま使う
            buildPoint = hit.point;
        }
        else
        {
            // 何にも当たらない場合は、
            // カメラ前方の一定距離を建築基準にする
            buildPoint =
                playerCamera.transform.position +
                playerCamera.transform.forward * buildDistance;

            // 地面の高さに落とす
            buildPoint.y = 0f;
        }

        // 壁専用のグリッド位置に変換する
        position = GetWallGridPosition(buildPoint, rotation);

        return true;
    }

    private Vector3 GetWallGridPosition(Vector3 buildPoint, Quaternion rotation)
    {
        // まず普通にグリッドへ吸着
        Vector3 pos = SnapToGrid(buildPoint);

        // 壁Prefabの中心が真ん中なので、
        // 地面から立つようにYを半分上げる
        pos.y += gridSize / 2f;

        // 壁の向きを取得
        float y = Mathf.Round(rotation.eulerAngles.y);

        // 壁はマスの中心ではなく、マスの辺に置く
        // 0度 / 180度の壁はZ方向の辺へずらす
        if (y == 0f || y == 180f)
        {
            pos.z += gridSize / 2f;
        }
        // 90度 / 270度の壁はX方向の辺へずらす
        else
        {
            pos.x += gridSize / 2f;
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

        // 90度単位で壁の向きを固定
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
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FloorEdit : MonoBehaviour, INetworkBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;

        [Tooltip("削除するマス。手前0,1／奥2,3")]
        public int[] removedCells;

        [Tooltip("未設定の場合は残るマスを自動生成")]
        public GameObject resultPrefab;
    }

    [Header("床のローカル寸法")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float depth = 1f;
    public float thickness = 1f;

    [Header("マテリアル")]
    public Material floorMaterial;
    public Material gridMaterial;

    [Header("編集パターン")]
    public List<Pattern> patterns = new List<Pattern>
    {
        new Pattern { name = "Corner 0", removedCells = new[] { 0 } },
        new Pattern { name = "Corner 1", removedCells = new[] { 1 } },
        new Pattern { name = "Corner 2", removedCells = new[] { 2 } },
        new Pattern { name = "Corner 3", removedCells = new[] { 3 } },
        new Pattern { name = "Half near", removedCells = new[] { 0, 1 } },
        new Pattern { name = "Half far", removedCells = new[] { 2, 3 } },
        new Pattern { name = "Half left", removedCells = new[] { 0, 2 } },
        new Pattern { name = "Half right", removedCells = new[] { 1, 3 } }
    };

    public Transform EditTransform =>
        this != null && isActiveAndEnabled ? transform : null;

    public bool IsSelectionActive => editing;

    private Renderer[] originalRenderers;
    private Collider[] originalColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;

    private GameObject result;
    private GameObject grid;

    private readonly Renderer[] tiles = new Renderer[4];
    private readonly bool[] visited = new bool[4];

    private int committedMask;
    private int selectionMask;
    private int editRevision;

    private bool strokeRemove;
    private bool editing;
    private float faceSign;
    private Camera editCamera;

    private void Awake()
    {
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        originalColliders = GetComponentsInChildren<Collider>(true);

        rendererStates = new bool[originalRenderers.Length];
        colliderStates = new bool[originalColliders.Length];

        for (int i = 0; i < originalRenderers.Length; i++)
            rendererStates[i] = originalRenderers[i].enabled;

        for (int i = 0; i < originalColliders.Length; i++)
            colliderStates[i] = originalColliders[i].enabled;
    }

    public bool BeginEdit(Camera camera)
    {
        if (editing || camera == null ||
            width <= 0f || depth <= 0f || thickness <= 0f)
        {
            return false;
        }

        NetworkBuildingEdit network =
            GetComponentInParent<NetworkBuildingEdit>();

        if (network == null || !network.Ready)
        {
            Debug.LogWarning(
                "床を編集できません：NetworkBuildingEditの設定と同期状態を確認してください。",
                this);
            return false;
        }

        if (gridMaterial == null)
        {
            Debug.LogWarning(
                "FloorEditのGrid Materialを設定してください。",
                this);
            return false;
        }

        editRevision = network.Revision;
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;

        Array.Clear(visited, 0, visited.Length);

        faceSign =
            transform.InverseTransformPoint(camera.transform.position).y
            >= localCenter.y ? 1f : -1f;

        grid = new GameObject("Floor Edit Grid");
        grid.transform.SetParent(transform, false);

        for (int i = 0; i < 4; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2;
            tile.transform.SetParent(grid.transform, false);

            tile.transform.localPosition =
                CellCenter(i) +
                Vector3.up * faceSign * (thickness / 2f + 0.015f);

            tile.transform.localScale = new Vector3(
                width / 2f * 0.94f,
                0.005f,
                depth / 2f * 0.94f);

            Collider col = tile.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);

            tiles[i] = tile.GetComponent<Renderer>();
            tiles[i].sharedMaterial = gridMaterial;
        }

        RefreshGrid();
        return true;
    }

    private Vector3 CellCenter(int index)
    {
        return localCenter + new Vector3(
            (index % 2 - 0.5f) * width / 2f,
            0f,
            (index / 2 - 0.5f) * depth / 2f);
    }

    public void Paint(Ray ray, bool startStroke)
    {
        if (!editing || grid == null)
            return;

        if (startStroke)
            Array.Clear(visited, 0, visited.Length);

        Vector3 origin = transform.InverseTransformPoint(ray.origin);
        Vector3 direction = transform.InverseTransformVector(ray.direction);

        if (Mathf.Abs(direction.y) < 0.00001f)
            return;

        float planeY =
            localCenter.y + faceSign * (thickness / 2f + 0.015f);

        float t = (planeY - origin.y) / direction.y;

        if (t < 0f)
            return;

        Vector3 point = origin + direction * t - localCenter;

        if (Mathf.Abs(point.x) >= width / 2f ||
            Mathf.Abs(point.z) >= depth / 2f)
        {
            return;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt((point.x / width + 0.5f) * 2f), 0, 1);

        int z = Mathf.Clamp(
            Mathf.FloorToInt((point.z / depth + 0.5f) * 2f), 0, 1);

        int cell = z * 2 + x;

        bool anyVisited = false;

        for (int i = 0; i < visited.Length; i++)
            anyVisited |= visited[i];

        if (!anyVisited)
            strokeRemove = (selectionMask & (1 << cell)) == 0;

        if (visited[cell])
            return;

        visited[cell] = true;

        if (strokeRemove)
            selectionMask |= 1 << cell;
        else
            selectionMask &= ~(1 << cell);

        RefreshGrid();
    }

    public static bool TryGetMask(int[] cells, out int mask)
    {
        mask = 0;

        if (cells == null)
            return false;

        foreach (int cell in cells)
        {
            if (cell < 0 || cell > 3)
                return false;

            mask |= 1 << cell;
        }

        return mask > 0 && mask < 15;
    }

    // 編集確定はサーバーに要求する。
    public bool ConfirmEdit(Transform player, out string reason)
    {
        reason = "";

        if (!editing)
        {
            reason = "編集中ではありません。";
            return false;
        }

        NetworkBuildingEdit network =
            GetComponentInParent<NetworkBuildingEdit>();

        if (network == null)
        {
            reason = "完成建築のルートにNetworkBuildingEditが必要です。";
            return false;
        }

        return network.RequestEdit(
            this,
            player,
            editRevision,
            selectionMask,
            new int[0],
            out reason);
    }

    public bool CommitNetworkSelection(
        int mask,
        int[] order,
        Transform player,
        out string reason)
    {
        reason = "";

        if (mask < 0 || mask >= 15)
        {
            reason = "床の選択内容が不正です。";
            return false;
        }

        CancelEdit();

        selectionMask = mask;
        editing = true;

        bool accepted = CommitEditLocal(player, out reason);

        if (!accepted)
            CancelEdit();

        return accepted;
    }

    public BuildingEditState CaptureNetworkState()
    {
        return new BuildingEditState
        {
            mask = committedMask,
            order = new int[0]
        };
    }

    public bool ApplyNetworkState(
        BuildingEditState snapshot,
        out string reason)
    {
        if (snapshot == null)
        {
            reason = "床の同期データがありません。";
            return false;
        }

        // サーバーで判定済みの結果を適用する。
        return CommitNetworkSelection(
            snapshot.mask,
            snapshot.order,
            null,
            out reason);
    }

    private bool CommitEditLocal(Transform player, out string reason)
    {
        reason = "";

        if (!editing)
        {
            reason = "編集中ではありません。";
            return false;
        }

        if (selectionMask == committedMask)
        {
            Finish();
            return true;
        }

        Pattern pattern = null;

        if (selectionMask != 0)
        {
            foreach (Pattern item in patterns)
            {
                if (item == null ||
                    !TryGetMask(item.removedCells, out int mask) ||
                    mask != selectionMask)
                {
                    continue;
                }

                if (pattern != null)
                {
                    reason = "同じ床パターンが重複登録されています。";
                    return false;
                }

                pattern = item;
            }

            if (pattern == null)
            {
                reason = "選択内容に対応する床パターンがありません。";
                return false;
            }
        }

        GameObject candidate = null;

        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern);

            if (candidate == null)
            {
                reason = "床のResult PrefabまたはColliderが不正です。";
                return false;
            }
        }

        Physics.SyncTransforms();

        Collider[] colliders = candidate != null
            ? candidate.GetComponentsInChildren<Collider>()
            : originalColliders;

        if (OverlapsPlayer(colliders, candidate == null, player))
        {
            if (candidate != null)
            {
                candidate.SetActive(false);
                Destroy(candidate);
            }

            reason = "編集後の床がPlayerに重なります。少し移動してください。";
            return false;
        }

        if (result != null)
        {
            result.SetActive(false);
            Destroy(result);
        }

        result = candidate;
        SetOriginal(selectionMask == 0);
        committedMask = selectionMask;

        Finish();
        return true;
    }

    private GameObject CreateResult(Pattern pattern)
    {
        if (!NetworkBuildingEdit.ValidateResultPrefab(
                pattern.resultPrefab, this))
        {
            return null;
        }

        GameObject root;

        if (pattern.resultPrefab != null)
        {
            root = Instantiate(pattern.resultPrefab, transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.SetActive(true);
        }
        else
        {
            root = new GameObject(pattern.name);
            root.transform.SetParent(transform, false);

            Material material = floorMaterial;

            if (material == null && originalRenderers.Length > 0)
                material = originalRenderers[0].sharedMaterial;

            for (int i = 0; i < 4; i++)
            {
                if ((selectionMask & (1 << i)) != 0)
                    continue;

                GameObject cell =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);

                cell.transform.SetParent(root.transform, false);
                cell.transform.localPosition = CellCenter(i);
                cell.transform.localScale =
                    new Vector3(width / 2f, thickness, depth / 2f);

                if (material != null)
                    cell.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = gameObject.layer;

        bool hasSolidCollider = false;

        foreach (Collider col in root.GetComponentsInChildren<Collider>())
            hasSolidCollider |= col.enabled && !col.isTrigger;

        if (!hasSolidCollider)
        {
            root.SetActive(false);
            Destroy(root);
            return null;
        }

        return root;
    }

    private bool OverlapsPlayer(
        Collider[] colliders,
        bool original,
        Transform player)
    {
        if (player == null)
            return false;

        Collider[] players = player.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null || col.isTrigger ||
                !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (original && !colliderStates[i])
                continue;

            if (!original && !col.enabled)
                continue;

            bool previousEnabled = col.enabled;

            col.enabled = true;
            Physics.SyncTransforms();

            Bounds bounds = col.bounds;
            col.enabled = previousEnabled;

            foreach (Collider body in players)
            {
                if (body.enabled && !body.isTrigger &&
                    PenetratesBounds(bounds, body.bounds))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PenetratesBounds(Bounds a, Bounds b)
    {
        const float tolerance = 0.005f;

        return
            Mathf.Min(a.max.x, b.max.x) -
            Mathf.Max(a.min.x, b.min.x) > tolerance &&
            Mathf.Min(a.max.y, b.max.y) -
            Mathf.Max(a.min.y, b.min.y) > tolerance &&
            Mathf.Min(a.max.z, b.max.z) -
            Mathf.Max(a.min.z, b.min.z) > tolerance;
    }

    private void SetOriginal(bool visible)
    {
        for (int i = 0; i < originalRenderers.Length; i++)
        {
            if (originalRenderers[i] != null)
                originalRenderers[i].enabled = visible && rendererStates[i];
        }

        for (int i = 0; i < originalColliders.Length; i++)
        {
            if (originalColliders[i] != null)
                originalColliders[i].enabled = visible && colliderStates[i];
        }
    }

    private void RefreshGrid()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null)
                continue;

            Color color = (selectionMask & (1 << i)) != 0
                ? new Color(1f, 0.35f, 0.1f)
                : new Color(0.1f, 0.65f, 1f);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            tiles[i].SetPropertyBlock(block);
        }
    }

    public void ResetSelection()
    {
        selectionMask = 0;

        if (editing)
            RefreshGrid();
    }

    public void CancelEdit()
    {
        selectionMask = committedMask;
        Finish();
    }

    private void Finish()
    {
        editing = false;

        if (grid != null)
        {
            grid.SetActive(false);
            Destroy(grid);
        }

        grid = null;
        editCamera = null;
    }

    private void OnDisable()
    {
        Finish();
    }

    private void OnGUI()
    {
        if (!editing || editCamera == null || grid == null)
            return;

        for (int i = 0; i < 4; i++)
        {
            Vector3 point = transform.TransformPoint(
                CellCenter(i) +
                Vector3.up * faceSign * (thickness / 2f + 0.025f));

            Vector3 screen = editCamera.WorldToScreenPoint(point);

            if (screen.z > 0f)
            {
                GUI.Label(
                    new Rect(screen.x - 6, Screen.height - screen.y - 10, 24, 24),
                    i.ToString());
            }
        }
    }
}
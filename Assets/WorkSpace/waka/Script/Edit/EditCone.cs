using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EditCone : MonoBehaviour, INetworkBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;

        [Tooltip("選択するマス。手前0,1／奥2,3")]
        public int[] selectedCells;

        [Tooltip("形状とColliderだけの編集結果Prefab")]
        public GameObject resultPrefab;
    }

    [Header("屋根のローカル寸法")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float depth = 1f;
    public float roofHeight = 2f;

    [Tooltip("編集マスだけのY回転")]
    public float gridRotationY = -45f;

    [Tooltip("編集開始時のカメラにパネルを正対させる")]
    public bool faceCameraOnEdit = true;

    [Tooltip("編集結果Prefabを単独配置したときの大きさを維持")]
    public bool preserveResultPrefabSize = true;

    [Header("マテリアル")]
    public Material gridMaterial;

    [Header("編集パターン")]
    public List<Pattern> patterns = new List<Pattern>
    {
        new Pattern { name = "Shape 0", selectedCells = new[] { 0 } },
        new Pattern { name = "Shape 0+1", selectedCells = new[] { 0, 1 } }
    };

    public Transform EditTransform =>
        this != null && isActiveAndEnabled ? transform : null;

    public bool IsSelectionActive => editing;

    private Renderer[] originalRenderers;
    private Collider[] originalColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;

    private Renderer[] editingRenderers;
    private bool[] editingRendererStates;

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

    private float GridOffset => faceSign * 0.02f;

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
            width <= 0f || depth <= 0f || roofHeight <= 0f)
        {
            return false;
        }

        NetworkBuildingEdit network =
            GetComponentInParent<NetworkBuildingEdit>();

        if (network == null || !network.Ready)
        {
            Debug.LogWarning(
                "屋根を編集できません：NetworkBuildingEditの設定と同期状態を確認してください。",
                this);
            return false;
        }

        if (gridMaterial == null)
        {
            Debug.LogWarning(
                "EditConeのGrid Materialを設定してください。",
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

        // 編集パネルを見やすくする。Colliderは残す。
        editingRenderers = result != null
            ? result.GetComponentsInChildren<Renderer>(true)
            : originalRenderers;

        editingRendererStates = new bool[editingRenderers.Length];

        for (int i = 0; i < editingRenderers.Length; i++)
        {
            if (editingRenderers[i] == null)
                continue;

            editingRendererStates[i] = editingRenderers[i].enabled;
            editingRenderers[i].enabled = false;
        }

        grid = new GameObject("Cone Edit Grid");
        grid.transform.SetParent(transform, false);
        grid.transform.localPosition = localCenter;
        grid.transform.localRotation =
            Quaternion.Euler(0f, gridRotationY, 0f);

        if (faceCameraOnEdit)
        {
            grid.transform.localPosition =
                localCenter + Vector3.up * (roofHeight / 2f);

            grid.transform.rotation = Quaternion.LookRotation(
                camera.transform.up,
                -camera.transform.forward);

            faceSign = 1f;
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2;
            tile.transform.SetParent(grid.transform, false);

            tile.transform.localPosition =
                CellCenter(i) + Vector3.up * GridOffset;

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
        return new Vector3(
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

        Vector3 origin = grid.transform.InverseTransformPoint(ray.origin);
        Vector3 direction = grid.transform.InverseTransformVector(ray.direction);

        if (Mathf.Abs(direction.y) < 0.00001f)
            return;

        float t = (GridOffset - origin.y) / direction.y;

        if (t < 0f)
            return;

        Vector3 point = origin + direction * t;

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

        return mask > 0 && mask <= 15;
    }

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

        if (mask < 0 || mask > 15)
        {
            reason = "屋根の選択内容が不正です。";
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
            reason = "屋根の同期データがありません。";
            return false;
        }

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
                    !TryGetMask(item.selectedCells, out int mask) ||
                    mask != selectionMask)
                {
                    continue;
                }

                if (pattern != null)
                {
                    reason = "同じ屋根パターンが重複登録されています。";
                    return false;
                }

                pattern = item;
            }

            if (pattern == null)
            {
                reason = "選択内容に対応する屋根パターンがありません。";
                return false;
            }
        }

        GameObject candidate = null;

        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern);

            if (candidate == null)
            {
                reason = "屋根のResult PrefabまたはColliderが不正です。";
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

            reason = "編集後の屋根がPlayerに重なります。少し移動してください。";
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

        if (pattern.resultPrefab == null)
        {
            Debug.LogWarning(
                "屋根のResult Prefabを設定してください：" + pattern.name,
                this);
            return null;
        }

        Vector3 parentScale = transform.lossyScale;

        if (preserveResultPrefabSize &&
            (Mathf.Abs(parentScale.x) < 0.00001f ||
             Mathf.Abs(parentScale.y) < 0.00001f ||
             Mathf.Abs(parentScale.z) < 0.00001f))
        {
            Debug.LogError("屋根の親Scaleに0を設定しないでください。", this);
            return null;
        }

        GameObject root =
            Instantiate(pattern.resultPrefab, transform, false);

        if (preserveResultPrefabSize)
        {
            Vector3 prefabScale = pattern.resultPrefab.transform.localScale;

            root.transform.localScale = new Vector3(
                prefabScale.x / parentScale.x,
                prefabScale.y / parentScale.y,
                prefabScale.z / parentScale.z);
        }

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.SetActive(true);

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

            foreach (Collider body in players)
            {
                if (!body.enabled || body.isTrigger)
                    continue;

                if (Physics.ComputePenetration(
                        body, body.transform.position, body.transform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 direction, out float distance) &&
                    distance > 0.005f)
                {
                    return true;
                }
            }
        }

        return false;
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

        if (editingRenderers != null)
        {
            for (int i = 0; i < editingRenderers.Length; i++)
            {
                if (editingRenderers[i] != null)
                    editingRenderers[i].enabled = editingRendererStates[i];
            }
        }

        editingRenderers = null;
        editingRendererStates = null;

        if (originalRenderers != null)
            SetOriginal(committedMask == 0);

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
            Vector3 point = grid.transform.TransformPoint(
                CellCenter(i) +
                Vector3.up * (GridOffset + faceSign * 0.01f));

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
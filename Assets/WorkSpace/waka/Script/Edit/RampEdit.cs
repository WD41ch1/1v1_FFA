using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RampEdit : MonoBehaviour, IBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;
        [Tooltip("Selected cells: bottom to top rows 0,1 / 2,3 / 4,5 / 6,7")]
        public int[] selectedCells;
        [Tooltip("Required shape-only prefab. Bottom-center pivot; same local coordinates as original ramp.")]
        public GameObject resultPrefab;
        [Tooltip("元階段に対する編集結果のY回転補正。通常0")]
        public float resultRotationY;
        [Tooltip("元階段ルート基準のローカル位置補正。通常0")]
        public Vector3 resultLocalOffset = Vector3.zero;
    }

    [Header("Local dimensions; Ramp local dimensions; pivot at bottom center")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float depth = 1f;
    public float rampHeight = 2f;
    [Tooltip("水平表示時の編集マスだけのY回転")]
    public float gridRotationY = 0f;
    [Tooltip("ON: 編集開始時のカメラに正対する8マス。OFF: 底面の水平パネル")]
    public bool faceCameraOnEdit = true;
    [Tooltip("ON: 結果Prefabを単独配置した大きさを維持。OFF: 元階段のScaleを継承")]
    public bool preserveResultPrefabSize = true;
    [Header("Materials")]
    public Material gridMaterial;
    [Header("Patterns; Result Prefab is required")]
    public List<Pattern> patterns = new List<Pattern>
    {
        new Pattern { name = "Shape 0", selectedCells = new [] { 0 } },
        new Pattern { name = "Shape 0+1", selectedCells = new [] { 0, 1 } }
    };

    public Transform EditTransform { get { return this != null && isActiveAndEnabled ? transform : null; } }
    private Renderer[] originalRenderers;
    private Collider[] originalColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;
    private GameObject result;
    private GameObject grid;
    private Material ownedGridMaterial;
    private readonly Renderer[] tiles = new Renderer[8];
    private readonly bool[] visited = new bool[8];
    private int committedMask;
    private int selectionMask;
    private bool strokeRemove;
    private bool editing;
    private float faceSign;
    private Camera editCamera;
    private Renderer[] editingRenderers;
    private bool[] editingRendererStates;
    private float GridOffset { get { return faceSign * 0.02f; } }

    private void Awake()
    {
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        originalColliders = GetComponentsInChildren<Collider>(true);
        rendererStates = new bool[originalRenderers.Length];
        colliderStates = new bool[originalColliders.Length];
        for (int i = 0; i < originalRenderers.Length; i++) rendererStates[i] = originalRenderers[i].enabled;
        for (int i = 0; i < originalColliders.Length; i++) colliderStates[i] = originalColliders[i].enabled;
    }

    public bool BeginEdit(Camera camera)
    {
        if (editing || width <= 0 || depth <= 0 || rampHeight <= 0) return false;
        Material material = gridMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) { Debug.LogError("RampEdit: assign Grid Material", this); return false; }
            ownedGridMaterial = new Material(shader);
            material = ownedGridMaterial;
        }
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;
        faceSign = transform.InverseTransformPoint(camera.transform.position).y >= localCenter.y ? 1f : -1f;
        // 底面のパネルが階段の斜面に隠れないよう、見た目だけを隠す。
        // Colliderは残すので編集中もPlayerは階段に乗れる。
        editingRenderers = result != null
            ? result.GetComponentsInChildren<Renderer>(true)
            : originalRenderers;
        editingRendererStates = new bool[editingRenderers.Length];
        for (int i = 0; i < editingRenderers.Length; i++)
        {
            if (editingRenderers[i] == null) continue;
            editingRendererStates[i] = editingRenderers[i].enabled;
            editingRenderers[i].enabled = false;
        }
        grid = new GameObject("Edit grid (visual only)");
        grid.transform.SetParent(transform, false);
        grid.transform.localPosition = localCenter;
        grid.transform.localRotation = Quaternion.Euler(0f, gridRotationY, 0f);
        if (faceCameraOnEdit)
        {
            // パネルは階段中央に置き、開始時のカメラに正対させる。
            grid.transform.localPosition = localCenter + Vector3.up * (rampHeight / 2f);
            Vector3 normal = -camera.transform.forward;
            Vector3 panelUp = camera.transform.up;
            grid.transform.rotation = Quaternion.LookRotation(panelUp, normal);
            faceSign = 1f;
        }
        for (int i = 0; i < 8; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2; // Ignore Raycast
            tile.transform.SetParent(grid.transform, false);
            tile.transform.localPosition = CellCenter(i) + Vector3.up * GridOffset;
            tile.transform.localScale = new Vector3(width / 2f * 0.94f, 0.005f, depth / 4f * 0.94f);
            Collider collider = tile.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            tiles[i] = tile.GetComponent<Renderer>();
            tiles[i].sharedMaterial = material;
        }
        RefreshGrid();
        return true;
    }

    private Vector3 CellCenter(int index)
    {
        return new Vector3((index % 2 - 0.5f) * width / 2f, 0f, (index / 2 - 1.5f) * depth / 4f);
    }

    public void Paint(Ray ray, bool startStroke)
    {
        if (!editing) return;
        if (startStroke) Array.Clear(visited, 0, visited.Length);
        Vector3 origin = grid.transform.InverseTransformPoint(ray.origin);
        Vector3 direction = grid.transform.InverseTransformVector(ray.direction);
        if (Mathf.Abs(direction.y) < 0.00001f) return;
        float t = (GridOffset - origin.y) / direction.y;
        if (t < 0) return;
        Vector3 point = origin + direction * t;
        if (Mathf.Abs(point.x) >= width / 2f || Mathf.Abs(point.z) >= depth / 2f) return;
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x / width + 0.5f) * 2f), 0, 1);
        int z = Mathf.Clamp(Mathf.FloorToInt((point.z / depth + 0.5f) * 4f), 0, 3);
        int cell = z * 2 + x;
        bool anyVisited = false;
        for (int i = 0; i < 8; i++) anyVisited |= visited[i];
        if (!anyVisited) strokeRemove = (selectionMask & (1 << cell)) == 0;
        if (visited[cell]) return;
        visited[cell] = true;
        if (strokeRemove) selectionMask |= 1 << cell;
        else selectionMask &= ~(1 << cell);
        RefreshGrid();
    }

    public static bool TryGetMask(int[] cells, out int mask)
    {
        mask = 0;
        if (cells == null) return false;
        foreach (int cell in cells)
        {
            if (cell < 0 || cell > 7) return false;
            mask |= 1 << cell;
        }
        return mask > 0 && mask <= 255;
    }

    public bool ConfirmEdit(Transform player, out string reason)
    {
        reason = "";
        if (!editing) { reason = "Not editing"; return false; }
        if (selectionMask == committedMask) { Finish(); return true; }
        Pattern pattern = null;
        if (selectionMask != 0)
        {
            foreach (Pattern item in patterns)
            {
                int mask;
                if (item != null && TryGetMask(item.selectedCells, out mask) && mask == selectionMask)
                {
                    if (pattern != null) { reason = "Duplicate pattern registration"; return false; }
                    pattern = item;
                }
            }
            if (pattern == null) { reason = "No pattern registered for this selection"; return false; }
        }

        GameObject candidate = null;
        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern);
            if (candidate == null) { reason = "Invalid result prefab"; return false; }
        }
        Physics.SyncTransforms();
        Collider[] candidateColliders = candidate != null ? candidate.GetComponentsInChildren<Collider>() : originalColliders;
        if (OverlapsPlayer(candidateColliders, candidate == null, player))
        {
            if (candidate != null) { candidate.SetActive(false); Destroy(candidate); }
            reason = "Player overlaps the new ramp; move away";
            return false;
        }
        if (result != null) { result.SetActive(false); Destroy(result); }
        result = candidate;
        SetOriginal(selectionMask == 0);
        committedMask = selectionMask;
        Finish();
        return true;
    }

    private GameObject CreateResult(Pattern pattern)
    {
        if (pattern.resultPrefab == null)
        {
            Debug.LogWarning("RampEdit: assign Result Prefab for " + pattern.name, this);
            return null;
        }
        foreach (MonoBehaviour script in pattern.resultPrefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script is IBuildingEditTarget)
            {
                Debug.LogError("Result Prefab must not contain editing components", this);
                return null;
            }
        }
        if (pattern.resultPrefab.GetComponentInChildren<Rigidbody>(true) != null)
        {
            Debug.LogError("Result Prefab must not contain a Rigidbody", this);
            return null;
        }
        Vector3 parentScale = transform.lossyScale;
        if (preserveResultPrefabSize &&
            (Mathf.Abs(parentScale.x) < 0.00001f ||
             Mathf.Abs(parentScale.y) < 0.00001f ||
             Mathf.Abs(parentScale.z) < 0.00001f))
        {
            Debug.LogError("RampEdit: parent scale must not contain zero", this);
            return null;
        }
        GameObject root = Instantiate(pattern.resultPrefab, transform, false);
        if (preserveResultPrefabSize)
        {
            // Prefab単独配置時のScaleを維持する。位置・回転は従来どおり。
            Vector3 prefabScale = pattern.resultPrefab.transform.localScale;
            root.transform.localScale = new Vector3(
                prefabScale.x / parentScale.x,
                prefabScale.y / parentScale.y,
                prefabScale.z / parentScale.z
            );
        }
        root.transform.localPosition = pattern.resultLocalOffset;
        root.transform.localRotation = Quaternion.Euler(0f, pattern.resultRotationY, 0f);
        root.SetActive(true);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
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

    private bool OverlapsPlayer(Collider[] colliders, bool original, Transform player)
    {
        if (player == null) return false;
        Collider[] players = player.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
            if (original && !colliderStates[i]) continue;
            if (!original && !col.enabled) continue;
            foreach (Collider body in players)
            {
                if (!body.enabled || body.isTrigger) continue;
                // Player should use CapsuleCollider, BoxCollider, SphereCollider
                // or a convex MeshCollider for this exact shape test.
                Vector3 direction;
                float distance;
                if (Physics.ComputePenetration(
                    body, body.transform.position, body.transform.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out direction, out distance) && distance > 0.005f)
                    return true;
            }
        }
        return false;
    }

    private void SetOriginal(bool visible)
    {
        for (int i = 0; i < originalRenderers.Length; i++)
            if (originalRenderers[i] != null) originalRenderers[i].enabled = visible && rendererStates[i];
        for (int i = 0; i < originalColliders.Length; i++)
            if (originalColliders[i] != null) originalColliders[i].enabled = visible && colliderStates[i];
    }

    private void RefreshGrid()
    {
        for (int i = 0; i < 8; i++)
        {
            Color color = (selectionMask & (1 << i)) != 0 ? new Color(1f, 0.35f, 0.1f) : new Color(0.1f, 0.65f, 1f);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            tiles[i].SetPropertyBlock(block);
        }
    }

    public void ResetSelection() { selectionMask = 0; if (editing) RefreshGrid(); }
    public void CancelEdit() { selectionMask = committedMask; Finish(); }
    private void Finish()
    {
        editing = false;
        if (editingRenderers != null)
        {
            for (int i = 0; i < editingRenderers.Length; i++)
                if (editingRenderers[i] != null)
                    editingRenderers[i].enabled = editingRendererStates[i];
        }
        editingRenderers = null;
        editingRendererStates = null;
        // 確定時は新しい状態、キャンセル時は編集前の状態に戻す。
        if (originalRenderers != null) SetOriginal(committedMask == 0);
        if (grid != null) { grid.SetActive(false); Destroy(grid); }
        grid = null;
        if (ownedGridMaterial != null) Destroy(ownedGridMaterial);
        ownedGridMaterial = null;
    }
    private void OnDisable() { Finish(); }

    private void OnGUI()
    {
        if (!editing || editCamera == null) return;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = grid.transform.TransformPoint(CellCenter(i) +
                Vector3.up * (GridOffset + faceSign * 0.01f));
            Vector3 screen = editCamera.WorldToScreenPoint(point);
            if (screen.z > 0)
                GUI.Label(new Rect(screen.x - 6, Screen.height - screen.y - 10, 24, 24), i.ToString());
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WallEdit : MonoBehaviour, IBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;
        [Tooltip("Removed cells: bottom row 0,1,2; middle 3,4,5; top 6,7,8")]
        public int[] removedCells;
        [Tooltip("Optional shape-only prefab. Center pivot; same local size as original wall.")]
        public GameObject resultPrefab;
    }

    [Header("Local dimensions; BuildWall expects center pivot and XY wall")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float height = 1f;
    public float thickness = 1f;
    [Header("Materials")]
    public Material wallMaterial;
    public Material gridMaterial;
    [Header("Patterns; null prefab generates remaining rectangular cells")]
    public List<Pattern> patterns = new List<Pattern>
    {
        new Pattern { name = "Window", removedCells = new [] { 4 } },
        new Pattern { name = "Doorway", removedCells = new [] { 1, 4 } }
    };

    public Transform EditTransform { get { return this != null && isActiveAndEnabled ? transform : null; } }
    private Renderer[] originalRenderers;
    private Collider[] originalColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;
    private GameObject result;
    private GameObject grid;
    private Material ownedGridMaterial;
    private readonly Renderer[] tiles = new Renderer[9];
    private readonly bool[] visited = new bool[9];
    private int committedMask;
    private int selectionMask;
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
        for (int i = 0; i < originalRenderers.Length; i++) rendererStates[i] = originalRenderers[i].enabled;
        for (int i = 0; i < originalColliders.Length; i++) colliderStates[i] = originalColliders[i].enabled;
    }

    public bool BeginEdit(Camera camera)
    {
        if (editing || width <= 0 || height <= 0 || thickness <= 0) return false;
        Material material = gridMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) { Debug.LogError("WallEdit: assign Grid Material", this); return false; }
            ownedGridMaterial = new Material(shader);
            material = ownedGridMaterial;
        }
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;
        faceSign = transform.InverseTransformPoint(camera.transform.position).z >= localCenter.z ? 1f : -1f;
        grid = new GameObject("Edit grid (visual only)");
        grid.transform.SetParent(transform, false);
        for (int i = 0; i < 9; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2; // Ignore Raycast
            tile.transform.SetParent(grid.transform, false);
            tile.transform.localPosition = CellCenter(i) + Vector3.forward * faceSign * (thickness / 2f + 0.015f);
            tile.transform.localScale = new Vector3(width / 3f * 0.94f, height / 3f * 0.94f, 0.005f);
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
        return localCenter + new Vector3((index % 3 - 1) * width / 3f, (index / 3 - 1) * height / 3f, 0f);
    }

    public void Paint(Ray ray, bool startStroke)
    {
        if (!editing) return;
        if (startStroke) Array.Clear(visited, 0, visited.Length);
        Vector3 origin = transform.InverseTransformPoint(ray.origin);
        Vector3 direction = transform.InverseTransformVector(ray.direction);
        if (Mathf.Abs(direction.z) < 0.00001f) return;
        float t = (localCenter.z + faceSign * (thickness / 2f + 0.015f) - origin.z) / direction.z;
        if (t < 0) return;
        Vector3 point = origin + direction * t - localCenter;
        if (Mathf.Abs(point.x) >= width / 2f || Mathf.Abs(point.y) >= height / 2f) return;
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x / width + 0.5f) * 3f), 0, 2);
        int y = Mathf.Clamp(Mathf.FloorToInt((point.y / height + 0.5f) * 3f), 0, 2);
        int cell = y * 3 + x;
        bool anyVisited = false;
        for (int i = 0; i < 9; i++) anyVisited |= visited[i];
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
            if (cell < 0 || cell > 8) return false;
            mask |= 1 << cell;
        }
        return mask > 0 && mask < 511;
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
                if (item != null && TryGetMask(item.removedCells, out mask) && mask == selectionMask)
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
            reason = "Player overlaps the new wall; move away";
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
        GameObject root;
        if (pattern.resultPrefab != null)
        {
            if (pattern.resultPrefab.GetComponentInChildren<WallEdit>(true) != null)
            {
                Debug.LogError("Result Prefab must contain shape and colliders only, not WallEdit", this);
                return null;
            }
            root = Instantiate(pattern.resultPrefab, transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.SetActive(true);
        }
        else
        {
            root = new GameObject(pattern.name);
            root.transform.SetParent(transform, false);
            Material material = wallMaterial;
            if (material == null && originalRenderers.Length > 0) material = originalRenderers[0].sharedMaterial;
            for (int i = 0; i < 9; i++)
            {
                if ((selectionMask & (1 << i)) != 0) continue;
                GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.transform.SetParent(root.transform, false);
                cell.transform.localPosition = CellCenter(i);
                cell.transform.localScale = new Vector3(width / 3f, height / 3f, thickness);
                if (material != null) cell.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
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
            bool enabledBefore = col.enabled;
            col.enabled = true;
            Physics.SyncTransforms();
            Bounds bounds = col.bounds;
            col.enabled = enabledBefore;
            foreach (Collider body in players)
                if (body.enabled && !body.isTrigger && bounds.Intersects(body.bounds)) return true;
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
        for (int i = 0; i < 9; i++)
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
        if (grid != null) { grid.SetActive(false); Destroy(grid); }
        grid = null;
        if (ownedGridMaterial != null) Destroy(ownedGridMaterial);
        ownedGridMaterial = null;
    }
    private void OnDisable() { Finish(); }

    private void OnGUI()
    {
        if (!editing || editCamera == null) return;
        for (int i = 0; i < 9; i++)
        {
            Vector3 point = transform.TransformPoint(CellCenter(i) +
                Vector3.forward * faceSign * (thickness / 2f + 0.025f));
            Vector3 screen = editCamera.WorldToScreenPoint(point);
            if (screen.z > 0)
                GUI.Label(new Rect(screen.x - 6, Screen.height - screen.y - 10, 24, 24), i.ToString());
        }
    }
}
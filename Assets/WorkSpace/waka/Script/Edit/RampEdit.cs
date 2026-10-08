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
        [Tooltip("Selected cells: bottom to top rows 0,1,2 / 3,4,5 / 6,7,8")]
        public int[] selectedCells;
        [Tooltip("Require the same selection order as Selected Cells")]
        public bool matchSelectionOrder;
        [Tooltip("Required shape-only prefab. Bottom-center pivot; same local coordinates as original ramp.")]
        public GameObject resultPrefab;
        [Tooltip("元階段に対する編集結果のY回転補正。通常0")]
        public float resultRotationY;
        [Tooltip("ON: 指定した子の形状のローカルX回転を変更。Positionは変えない")]
        public bool useResultRotationX;
        [Tooltip("傾斜を付けている子のパス。例: Ramp_Cone、Visual/Ramp。ルート指定は不可")]
        public string resultRotationTargetPath = "";
        [Tooltip("対象の子のローカルX回転。元が-45なら45で上り方向を反転")]
        public float resultRotationX = 45f;
        [Tooltip("元階段ルート基準のローカル位置補正。通常0")]
        public Vector3 resultLocalOffset = Vector3.zero;
    }

    [Header("Local dimensions; Ramp local dimensions; pivot at bottom center")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    [Header("水平編集パネル（ワールド寸法）")]
    public Vector2 panelSize = new Vector2(4f, 4f);
    public float depth = 1f;
    public float rampHeight = 2f;
    [Tooltip("水平表示時の編集マスだけのY回転")]
    public float gridRotationY = 0f;
    [Tooltip("ON: 編集開始時のカメラに正対する9マス。OFF: 底面の水平パネル")]
    public bool faceCameraOnEdit = false;
    [Tooltip("ON: 結果Prefabを単独配置した大きさを維持。OFF: 元階段のScaleを継承")]
    public bool preserveResultPrefabSize = true;
    [Header("リセット時の上り方向")]
    public bool keepDirectionOnReset = true;
    [Tooltip("通常階段Prefab内の、X回転で傾斜を付けた子。形状とColliderを一緒に回せるTransform")]
    public Transform resetSlopeTarget;
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
    private MeshFilter[] originalMeshes;
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
    private readonly List<int> selectionOrder = new List<int>();
    private readonly List<int> committedOrder = new List<int>();
    private bool replaceOnFirstCell;
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
        originalMeshes = GetComponentsInChildren<MeshFilter>();
        originalColliders = GetComponentsInChildren<Collider>(true);
        rendererStates = new bool[originalRenderers.Length];
        colliderStates = new bool[originalColliders.Length];
        for (int i = 0; i < originalRenderers.Length; i++) rendererStates[i] = originalRenderers[i].enabled;
        for (int i = 0; i < originalColliders.Length; i++) colliderStates[i] = originalColliders[i].enabled;
    }

    public bool BeginEdit(Camera camera)
    {
        if (editing || panelSize.x <= 0 || panelSize.y <= 0) return false;
        Material material = gridMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) { Debug.LogError("RampEdit: assign Grid Material", this); return false; }
            ownedGridMaterial = new Material(shader);
            material = ownedGridMaterial;
        }
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;
        selectionOrder.Clear();
        selectionOrder.AddRange(committedOrder);
        replaceOnFirstCell = false;
        foreach (Pattern item in patterns)
            if (item != null && item.matchSelectionOrder) replaceOnFirstCell = true;
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
        // 階段ルートの傾き・非均一Scaleを引き継がない。
        // Local Centerは既存と同じ底面中央の指定。向きは階段ルートに固定。
        Vector3 panelPosition = transform.TransformPoint(localCenter);
        panelPosition.y = GetBottomWorldY(panelPosition.y);
        grid.transform.position = panelPosition;
        // 階段の向きで固定。カメラの位置・向きは番号配置に使わない。
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            Vector3 right = transform.right;
            right.y = 0f;
            forward = Vector3.Cross(right, Vector3.up);
        }
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        grid.transform.rotation = Quaternion.Euler(0f, yaw + gridRotationY, 0f);
        grid.transform.localScale = Vector3.one;
        faceSign = camera.transform.position.y >= grid.transform.position.y ? 1f : -1f;
        for (int i = 0; i < 9; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2; // Ignore Raycast
            tile.transform.SetParent(grid.transform, false);
            tile.transform.localPosition = CellCenter(i) + Vector3.up * GridOffset;
            tile.transform.localScale = new Vector3(panelSize.x / 3f * 0.94f, 0.005f, panelSize.y / 3f * 0.94f);
            Collider collider = tile.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            tiles[i] = tile.GetComponent<Renderer>();
            tiles[i].sharedMaterial = material;
            tiles[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tiles[i].receiveShadows = false;

            // A slightly larger white backing gives each tile a visible border.
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "Cell border " + i;
            border.layer = 2;
            border.transform.SetParent(grid.transform, false);
            border.transform.localPosition = CellCenter(i) +
                Vector3.up * (GridOffset - faceSign * 0.005f);
            border.transform.localScale = new Vector3(
                panelSize.x / 3f * 0.98f, 0.003f, panelSize.y / 3f * 0.98f
            );
            Collider borderCollider = border.GetComponent<Collider>();
            borderCollider.enabled = false;
            Destroy(borderCollider);
            Renderer borderRenderer = border.GetComponent<Renderer>();
            borderRenderer.sharedMaterial = material;
            borderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            borderRenderer.receiveShadows = false;
            MaterialPropertyBlock white = new MaterialPropertyBlock();
            white.SetColor("_Color", Color.white);
            white.SetColor("_BaseColor", Color.white);
            borderRenderer.SetPropertyBlock(white);
        }
        RefreshGrid();
        return true;
    }

    // 元階段の形状からワールド下端を取得。編集後も同じ建築階層を維持。
    private float GetBottomWorldY(float fallback)
    {
        float bottom = float.PositiveInfinity;
        foreach (MeshFilter source in originalMeshes)
        {
            if (source == null || source.sharedMesh == null) continue;
            Renderer renderer = source.GetComponent<Renderer>();
            int index = Array.IndexOf(originalRenderers, renderer);
            if (index < 0 || !rendererStates[index]) continue;
            Mesh mesh = source.sharedMesh;
            if (mesh.isReadable)
            {
                foreach (Vector3 vertex in mesh.vertices)
                    bottom = Mathf.Min(bottom, source.transform.TransformPoint(vertex).y);
            }
            else
            {
                // 読み取り不可メッシュはローカルBoundsの8頂点で近似。
                Bounds bounds = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z
                    );
                    bottom = Mathf.Min(bottom, source.transform.TransformPoint(corner).y);
                }
            }
        }
        return float.IsPositiveInfinity(bottom) ? fallback : bottom;
    }
    private Vector3 CellCenter(int index)
    {
        return new Vector3((index % 3 - 1f) * panelSize.x / 3f, 0f, (index / 3 - 1f) * panelSize.y / 3f);
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
        if (Mathf.Abs(point.x) >= panelSize.x / 2f || Mathf.Abs(point.z) >= panelSize.y / 2f) return;
        int x = Mathf.Clamp(Mathf.FloorToInt((point.x / panelSize.x + 0.5f) * 3f), 0, 2);
        int z = Mathf.Clamp(Mathf.FloorToInt((point.z / panelSize.y + 0.5f) * 3f), 0, 2);
        int cell = z * 3 + x;
        // On re-edit, the first valid cell starts a fresh ordered selection.
        if (replaceOnFirstCell)
        {
            selectionMask = 0;
            selectionOrder.Clear();
            Array.Clear(visited, 0, visited.Length);
            replaceOnFirstCell = false;
        }
        bool anyVisited = false;
        for (int i = 0; i < 9; i++) anyVisited |= visited[i];
        if (!anyVisited) strokeRemove = (selectionMask & (1 << cell)) == 0;
        if (visited[cell]) return;
        visited[cell] = true;
        if (strokeRemove)
        {
            selectionMask |= 1 << cell;
            if (!selectionOrder.Contains(cell)) selectionOrder.Add(cell);
        }
        else
        {
            selectionMask &= ~(1 << cell);
            selectionOrder.Remove(cell);
        }
        RefreshGrid();
    }

    public static bool SameOrder(IList<int> a, IList<int> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
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
        return mask > 0 && mask <= 511;
    }

    public bool ConfirmEdit(Transform player, out string reason)
    {
        reason = "";
        if (!editing) { reason = "Not editing"; return false; }
        if (selectionMask == committedMask && SameOrder(selectionOrder, committedOrder)) { Finish(); return true; }
        Pattern pattern = null;
        int bestPriority = -1;
        int matches = 0;
        if (selectionMask != 0)
        {
            foreach (Pattern item in patterns)
            {
                int mask;
                if (item != null && TryGetMask(item.selectedCells, out mask) && mask == selectionMask)
                {
                    if (item.matchSelectionOrder && !SameOrder(selectionOrder, item.selectedCells)) continue;
                    int priority = item.matchSelectionOrder ? 1 : 0;
                    if (priority > bestPriority)
                    {
                        bestPriority = priority;
                        pattern = item;
                        matches = 1;
                    }
                    else if (priority == bestPriority) matches++;
                }
            }
            if (pattern == null) { reason = "No pattern registered for this selection/order"; return false; }
            if (matches > 1) { reason = "Duplicate pattern registration"; return false; }
        }

        GameObject candidate = null;
        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern);
            if (candidate == null) { reason = "Invalid result prefab"; return false; }
        }
        Quaternion beforeResetRotation = Quaternion.identity;
        bool changedResetRotation = false;
        Vector3 beforeResetPosition = Vector3.zero;
        if (selectionMask == 0 && keepDirectionOnReset && result != null)
        {
            Transform target = ResolveResetSlopeTarget();
            Vector3 originalUp, editedUp;
            if (target == null)
            {
                reason = "Reset blocked: assign Reset Slope Target to the original slope";
                return false;
            }
            if (!TryGetUphill(originalMeshes, out originalUp) ||
                !TryGetUphill(result.GetComponentsInChildren<MeshFilter>(), out editedUp))
            {
                reason = "Reset blocked: cannot read slope direction; enable Read/Write on the ramp mesh";
                return false;
            }
            resetSlopeTarget = target;
            beforeResetRotation = target.localRotation;
            beforeResetPosition = target.localPosition;
            Vector3 center = GetOriginalCenter(target);
            float yaw = Vector3.SignedAngle(originalUp, editedUp, Vector3.up);
            // Match the actual uphill direction. Preserve pitch, size and footprint center.
            target.RotateAround(center, Vector3.up, yaw);
            changedResetRotation = true;
        }
        Physics.SyncTransforms();
        Collider[] candidateColliders = candidate != null ? candidate.GetComponentsInChildren<Collider>() : originalColliders;
        if (OverlapsPlayer(candidateColliders, candidate == null, player))
        {
            if (candidate != null) { candidate.SetActive(false); Destroy(candidate); }
            if (changedResetRotation)
            {
                resetSlopeTarget.localRotation = beforeResetRotation;
                resetSlopeTarget.localPosition = beforeResetPosition;
                Physics.SyncTransforms();
            }
            reason = "Player overlaps the new ramp; move away";
            return false;
        }
        if (result != null) { result.SetActive(false); Destroy(result); }
        result = candidate;
        SetOriginal(selectionMask == 0);
        committedMask = selectionMask;
        committedOrder.Clear();
        committedOrder.AddRange(selectionOrder);
        Finish();
        return true;
    }

    // Use the largest sloping triangle in world space. For a thin Cube ramp,
    // the broad face is larger than its edge faces. Orient its normal upwards.
    private static bool TryGetUphill(MeshFilter[] meshes, out Vector3 uphill)
    {
        uphill = Vector3.zero;
        float largestArea = 0f;
        foreach (MeshFilter source in meshes)
        {
            if (source == null || !source.gameObject.activeInHierarchy) continue;
            Mesh mesh = source.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = source.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = source.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = source.transform.TransformPoint(vertices[triangles[i + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude;
                if (area <= largestArea || area < 0.000001f) continue;
                Vector3 normal = cross / area;
                if (normal.y < 0f) normal = -normal;
                if (normal.y < 0.1f || normal.y > 0.995f) continue;
                uphill = new Vector3(-normal.x, 0f, -normal.z).normalized;
                largestArea = area;
            }
        }
        return largestArea > 0f;
    }

    private Vector3 GetOriginalCenter(Transform target)
    {
        bool found = false;
        Bounds world = new Bounds();
        foreach (MeshFilter source in originalMeshes)
        {
            if (source == null || source.sharedMesh == null) continue;
            if (source.transform != target && !source.transform.IsChildOf(target)) continue;
            Bounds bounds = source.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = source.transform.TransformPoint(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                if (!found) { world = new Bounds(point, Vector3.zero); found = true; }
                else world.Encapsulate(point);
            }
        }
        return found ? world.center : target.position;
    }

    private Transform ResolveResetSlopeTarget()
    {
        if (resetSlopeTarget != null)
        {
            if ((resetSlopeTarget == transform || resetSlopeTarget.IsChildOf(transform)) &&
                (result == null || !resetSlopeTarget.IsChildOf(result.transform)))
                return resetSlopeTarget;
            return null; // Do not silently replace an explicitly incorrect reference.
        }

        // Only infer when the original shape has one unambiguous mesh/Collider target.
        Transform candidate = null;
        foreach (MeshFilter mesh in originalMeshes)
        {
            if (mesh == null || mesh.sharedMesh == null) continue;
            Renderer renderer = mesh.GetComponent<Renderer>();
            int index = Array.IndexOf(originalRenderers, renderer);
            if (index < 0 || !rendererStates[index]) continue;
            if (candidate != null && candidate != mesh.transform) return null;
            candidate = mesh.transform;
        }
        if (candidate == null) return null;
        for (int i = 0; i < originalColliders.Length; i++)
        {
            Collider col = originalColliders[i];
            if (col == null || !colliderStates[i] || col.isTrigger) continue;
            if (col.transform != candidate && !col.transform.IsChildOf(candidate)) return null;
        }
        return candidate;
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
        if (pattern.useResultRotationX)
        {
            Transform slope = string.IsNullOrWhiteSpace(pattern.resultRotationTargetPath)
                ? null : root.transform.Find(pattern.resultRotationTargetPath);
            if (slope == null)
            {
                Debug.LogError("RampEdit: Result Rotation Target Path must identify the slope child", this);
                root.SetActive(false);
                Destroy(root);
                return null;
            }
            Vector3 angles = slope.localEulerAngles;
            angles.x = pattern.resultRotationX;
            slope.localRotation = Quaternion.Euler(angles);
        }
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
        for (int i = 0; i < 9; i++)
        {
            Color color = (selectionMask & (1 << i)) != 0 ? new Color(1f, 0.35f, 0.1f) : new Color(0.05f, 0.8f, 0.95f);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            tiles[i].SetPropertyBlock(block);
        }
    }

    public void ResetSelection() { selectionMask = 0; selectionOrder.Clear(); replaceOnFirstCell = false; if (editing) RefreshGrid(); }
    public void CancelEdit() { selectionMask = committedMask; selectionOrder.Clear(); selectionOrder.AddRange(committedOrder); Finish(); }
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
        if (ownedGridMaterial != null) Destroy(ownedGridMaterial);
        ownedGridMaterial = null;
    }
    private void OnDisable() { Finish(); }

    private void OnGUI()
    {
        if (!editing || editCamera == null) return;
        GUI.Label(new Rect(10, 80, 600, 25), "Order: " + string.Join(" > ", selectionOrder));
        for (int i = 0; i < 9; i++)
        {
            Vector3 point = grid.transform.TransformPoint(CellCenter(i) +
                Vector3.up * (GridOffset + faceSign * 0.01f));
            Vector3 screen = editCamera.WorldToScreenPoint(point);
            if (screen.z > 0)
                GUI.Label(new Rect(screen.x - 6, Screen.height - screen.y - 10, 24, 24), i.ToString());
        }
    }
}
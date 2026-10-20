using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RampEdit : MonoBehaviour, INetworkBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;

        [Tooltip("選択マス。手前0,1,2／中央3,4,5／奥6,7,8")]
        public int[] selectedCells;

        [Tooltip("Selected Cellsに指定した順番まで一致させる")]
        public bool matchSelectionOrder;

        [Tooltip("形状とColliderだけの編集結果Prefab")]
        public GameObject resultPrefab;

        [Tooltip("編集結果のY回転補正")]
        public float resultRotationY;

        [Tooltip("指定した子のローカルX回転を変更")]
        public bool useResultRotationX;

        [Tooltip("傾斜を持つ子のパス。例：Ramp_Cone")]
        public string resultRotationTargetPath = "";

        public float resultRotationX = 45f;
        public Vector3 resultLocalOffset = Vector3.zero;
    }

    [Header("ローカル寸法")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float depth = 1f;
    public float rampHeight = 2f;

    [Header("水平編集パネルのワールド寸法")]
    public Vector2 panelSize = new Vector2(4f, 4f);
    public float gridRotationY = 0f;

    // 既存Inspectorの値を維持するため残す。
    // この版の階段パネルは水平固定。
    public bool faceCameraOnEdit = false;

    [Tooltip("編集結果Prefabを単独配置したときの大きさを維持")]
    public bool preserveResultPrefabSize = true;

    [Header("リセット時の上り方向")]
    public bool keepDirectionOnReset = true;

    [Tooltip("元階段の傾斜とColliderをまとめて回せるTransform")]
    public Transform resetSlopeTarget;

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
    private MeshFilter[] originalMeshes;
    private Collider[] originalColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;

    private Renderer[] editingRenderers;
    private bool[] editingRendererStates;

    private GameObject result;
    private GameObject grid;

    private readonly Renderer[] tiles = new Renderer[9];
    private readonly bool[] visited = new bool[9];
    private readonly List<int> selectionOrder = new List<int>();
    private readonly List<int> committedOrder = new List<int>();

    private int committedMask;
    private int selectionMask;
    private int editRevision;

    private bool replaceOnFirstCell;
    private bool strokeRemove;
    private bool editing;
    private float faceSign;
    private Camera editCamera;

    private float GridOffset => faceSign * 0.02f;

    private void Awake()
    {
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        originalMeshes = GetComponentsInChildren<MeshFilter>();
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
            panelSize.x <= 0f || panelSize.y <= 0f)
        {
            return false;
        }

        NetworkBuildingEdit network =
            GetComponentInParent<NetworkBuildingEdit>();

        if (network == null || !network.Ready)
        {
            Debug.LogWarning(
                "階段を編集できません：NetworkBuildingEditの設定と同期状態を確認してください。",
                this);
            return false;
        }

        if (gridMaterial == null)
        {
            Debug.LogWarning(
                "RampEditのGrid Materialを設定してください。",
                this);
            return false;
        }

        editRevision = network.Revision;
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;

        selectionOrder.Clear();
        selectionOrder.AddRange(committedOrder);
        Array.Clear(visited, 0, visited.Length);

        replaceOnFirstCell = false;

        foreach (Pattern item in patterns)
        {
            if (item != null && item.matchSelectionOrder)
                replaceOnFirstCell = true;
        }

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

        // 傾きや非均一Scaleを継承しない水平パネル。
        grid = new GameObject("Ramp Edit Grid");

        Vector3 panelPosition = transform.TransformPoint(localCenter);
        panelPosition.y = GetBottomWorldY(panelPosition.y);
        grid.transform.position = panelPosition;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            Vector3 right = transform.right;
            right.y = 0f;
            forward = Vector3.Cross(right, Vector3.up);
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        grid.transform.rotation =
            Quaternion.Euler(0f, yaw + gridRotationY, 0f);

        grid.transform.localScale = Vector3.one;

        faceSign =
            camera.transform.position.y >= grid.transform.position.y
            ? 1f : -1f;

        for (int i = 0; i < 9; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Cell " + i;
            tile.layer = 2;
            tile.transform.SetParent(grid.transform, false);

            tile.transform.localPosition =
                CellCenter(i) + Vector3.up * GridOffset;

            tile.transform.localScale = new Vector3(
                panelSize.x / 3f * 0.94f,
                0.005f,
                panelSize.y / 3f * 0.94f);

            Collider col = tile.GetComponent<Collider>();
            col.enabled = false;
            Destroy(col);

            tiles[i] = tile.GetComponent<Renderer>();
            tiles[i].sharedMaterial = gridMaterial;
            tiles[i].shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            tiles[i].receiveShadows = false;

            GameObject border =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            border.name = "Cell Border " + i;
            border.layer = 2;
            border.transform.SetParent(grid.transform, false);

            border.transform.localPosition =
                CellCenter(i) +
                Vector3.up * (GridOffset - faceSign * 0.005f);

            border.transform.localScale = new Vector3(
                panelSize.x / 3f * 0.98f,
                0.003f,
                panelSize.y / 3f * 0.98f);

            Collider borderCollider = border.GetComponent<Collider>();
            borderCollider.enabled = false;
            Destroy(borderCollider);

            Renderer borderRenderer = border.GetComponent<Renderer>();
            borderRenderer.sharedMaterial = gridMaterial;
            borderRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            borderRenderer.receiveShadows = false;

            MaterialPropertyBlock white = new MaterialPropertyBlock();
            white.SetColor("_Color", Color.white);
            white.SetColor("_BaseColor", Color.white);
            borderRenderer.SetPropertyBlock(white);
        }

        RefreshGrid();
        return true;
    }

    private float GetBottomWorldY(float fallback)
    {
        float bottom = float.PositiveInfinity;

        foreach (MeshFilter source in originalMeshes)
        {
            if (source == null || source.sharedMesh == null)
                continue;

            Renderer renderer = source.GetComponent<Renderer>();
            int index = Array.IndexOf(originalRenderers, renderer);

            if (index < 0 || !rendererStates[index])
                continue;

            Mesh mesh = source.sharedMesh;

            if (mesh.isReadable)
            {
                foreach (Vector3 vertex in mesh.vertices)
                {
                    bottom = Mathf.Min(
                        bottom,
                        source.transform.TransformPoint(vertex).y);
                }
            }
            else
            {
                Bounds bounds = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);

                    bottom = Mathf.Min(
                        bottom,
                        source.transform.TransformPoint(corner).y);
                }
            }
        }

        return float.IsPositiveInfinity(bottom) ? fallback : bottom;
    }

    private Vector3 CellCenter(int index)
    {
        return new Vector3(
            (index % 3 - 1f) * panelSize.x / 3f,
            0f,
            (index / 3 - 1f) * panelSize.y / 3f);
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

        if (Mathf.Abs(point.x) >= panelSize.x / 2f ||
            Mathf.Abs(point.z) >= panelSize.y / 2f)
        {
            return;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt((point.x / panelSize.x + 0.5f) * 3f), 0, 2);

        int z = Mathf.Clamp(
            Mathf.FloorToInt((point.z / panelSize.y + 0.5f) * 3f), 0, 2);

        int cell = z * 3 + x;

        if (replaceOnFirstCell)
        {
            selectionMask = 0;
            selectionOrder.Clear();
            Array.Clear(visited, 0, visited.Length);
            replaceOnFirstCell = false;
        }

        bool anyVisited = false;

        for (int i = 0; i < visited.Length; i++)
            anyVisited |= visited[i];

        if (!anyVisited)
            strokeRemove = (selectionMask & (1 << cell)) == 0;

        if (visited[cell])
            return;

        visited[cell] = true;

        if (strokeRemove)
        {
            selectionMask |= 1 << cell;

            if (!selectionOrder.Contains(cell))
                selectionOrder.Add(cell);
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
        if (a == null || b == null || a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    public static bool TryGetMask(int[] cells, out int mask)
    {
        mask = 0;

        if (cells == null)
            return false;

        foreach (int cell in cells)
        {
            if (cell < 0 || cell > 8)
                return false;

            mask |= 1 << cell;
        }

        return mask > 0 && mask <= 511;
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
            selectionOrder.ToArray(),
            out reason);
    }

    public bool CommitNetworkSelection(
        int mask,
        int[] order,
        Transform player,
        out string reason)
    {
        reason = "";

        if (mask < 0 || mask > 511)
        {
            reason = "階段の選択内容が不正です。";
            return false;
        }

        if (order == null || order.Length > 9)
        {
            reason = "階段の選択順が不正です。";
            return false;
        }

        int orderMask = 0;

        foreach (int cell in order)
        {
            if (cell < 0 || cell > 8 ||
                (orderMask & (1 << cell)) != 0)
            {
                reason = "階段の選択順が不正です。";
                return false;
            }

            orderMask |= 1 << cell;
        }

        if (orderMask != mask)
        {
            reason = "階段の選択マスと順番が一致しません。";
            return false;
        }

        CancelEdit();

        selectionMask = mask;
        selectionOrder.Clear();
        selectionOrder.AddRange(order);
        editing = true;

        bool accepted = CommitEditLocal(player, out reason);

        if (!accepted)
            CancelEdit();

        return accepted;
    }

    public BuildingEditState CaptureNetworkState()
    {
        BuildingEditState snapshot = new BuildingEditState
        {
            mask = committedMask,
            order = committedOrder.ToArray()
        };

        Transform slope = ResolveResetSlopeTarget();
        int[] path = BuildingEditTransformPath.Capture(transform, slope);

        if (path != null)
        {
            snapshot.hasSlopePose = true;
            snapshot.slopePath = path;
            snapshot.slopePosition = slope.localPosition;
            snapshot.slopeRotation = slope.localRotation;
        }

        return snapshot;
    }

    public bool ApplyNetworkState(
        BuildingEditState snapshot,
        out string reason)
    {
        reason = "";

        if (snapshot == null)
        {
            reason = "階段の同期データがありません。";
            return false;
        }

        CancelEdit();

        // 途中参加でも同じ向きになるよう絶対位置・回転を反映。
        if (snapshot.hasSlopePose)
        {
            Transform slope = BuildingEditTransformPath.Resolve(
                transform, snapshot.slopePath);

            if (slope == null)
            {
                reason = "階段のReset Slope Targetの階層が一致しません。";
                return false;
            }

            slope.localPosition = snapshot.slopePosition;
            slope.localRotation = snapshot.slopeRotation;
            resetSlopeTarget = slope;
        }

        bool previousKeepDirection = keepDirectionOnReset;
        keepDirectionOnReset = false;

        try
        {
            return CommitNetworkSelection(
                snapshot.mask,
                snapshot.order ?? new int[0],
                null,
                out reason);
        }
        finally
        {
            keepDirectionOnReset = previousKeepDirection;
        }
    }

    private bool CommitEditLocal(Transform player, out string reason)
    {
        reason = "";

        if (!editing)
        {
            reason = "編集中ではありません。";
            return false;
        }

        if (selectionMask == committedMask &&
            SameOrder(selectionOrder, committedOrder))
        {
            Finish();
            return true;
        }

        Pattern pattern = null;
        int bestPriority = -1;
        int matches = 0;

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

                if (item.matchSelectionOrder &&
                    !SameOrder(selectionOrder, item.selectedCells))
                {
                    continue;
                }

                int priority = item.matchSelectionOrder ? 1 : 0;

                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    pattern = item;
                    matches = 1;
                }
                else if (priority == bestPriority)
                {
                    matches++;
                }
            }

            if (pattern == null)
            {
                reason = "選択マス・順番に対応する階段パターンがありません。";
                return false;
            }

            if (matches > 1)
            {
                reason = "階段パターンが重複登録されています。";
                return false;
            }
        }

        GameObject candidate = null;

        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern);

            if (candidate == null)
            {
                reason = "階段のResult PrefabまたはColliderが不正です。";
                return false;
            }
        }

        Quaternion beforeResetRotation = Quaternion.identity;
        Vector3 beforeResetPosition = Vector3.zero;
        bool changedResetRotation = false;

        if (selectionMask == 0 && keepDirectionOnReset && result != null)
        {
            Transform target = ResolveResetSlopeTarget();

            if (target == null)
            {
                reason = "Reset Slope Targetに元階段の傾斜部分を設定してください。";
                return false;
            }

            if (!TryGetUphill(originalMeshes, out Vector3 originalUp) ||
                !TryGetUphill(
                    result.GetComponentsInChildren<MeshFilter>(),
                    out Vector3 editedUp))
            {
                reason = "階段の方向を取得できません。MeshのRead/Writeを確認してください。";
                return false;
            }

            resetSlopeTarget = target;
            beforeResetRotation = target.localRotation;
            beforeResetPosition = target.localPosition;

            Vector3 center = GetOriginalCenter(target);
            float yaw = Vector3.SignedAngle(originalUp, editedUp, Vector3.up);

            target.RotateAround(center, Vector3.up, yaw);
            changedResetRotation = true;
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

            if (changedResetRotation)
            {
                resetSlopeTarget.localRotation = beforeResetRotation;
                resetSlopeTarget.localPosition = beforeResetPosition;
                Physics.SyncTransforms();
            }

            reason = "編集後の階段がPlayerに重なります。少し移動してください。";
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
        committedOrder.Clear();
        committedOrder.AddRange(selectionOrder);

        Finish();
        return true;
    }

    private static bool TryGetUphill(
        MeshFilter[] meshes,
        out Vector3 uphill)
    {
        uphill = Vector3.zero;
        float largestArea = 0f;

        foreach (MeshFilter source in meshes)
        {
            if (source == null || !source.gameObject.activeInHierarchy)
                continue;

            Mesh mesh = source.sharedMesh;

            if (mesh == null || !mesh.isReadable)
                continue;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = source.transform.TransformPoint(
                    vertices[triangles[i]]);

                Vector3 b = source.transform.TransformPoint(
                    vertices[triangles[i + 1]]);

                Vector3 c = source.transform.TransformPoint(
                    vertices[triangles[i + 2]]);

                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude;

                if (area <= largestArea || area < 0.000001f)
                    continue;

                Vector3 normal = cross / area;

                if (normal.y < 0f)
                    normal = -normal;

                if (normal.y < 0.1f || normal.y > 0.995f)
                    continue;

                uphill = new Vector3(
                    -normal.x, 0f, -normal.z).normalized;

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
            if (source == null || source.sharedMesh == null)
                continue;

            if (source.transform != target &&
                !source.transform.IsChildOf(target))
            {
                continue;
            }

            Bounds bounds = source.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 point = source.transform.TransformPoint(
                    new Vector3(
                        (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z));

                if (!found)
                {
                    world = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else
                {
                    world.Encapsulate(point);
                }
            }
        }

        return found ? world.center : target.position;
    }

    private Transform ResolveResetSlopeTarget()
    {
        if (resetSlopeTarget != null)
        {
            bool inOriginalHierarchy =
                resetSlopeTarget == transform ||
                resetSlopeTarget.IsChildOf(transform);

            bool inResult =
                result != null &&
                resetSlopeTarget.IsChildOf(result.transform);

            return inOriginalHierarchy && !inResult
                ? resetSlopeTarget
                : null;
        }

        Transform candidate = null;

        foreach (MeshFilter mesh in originalMeshes)
        {
            if (mesh == null || mesh.sharedMesh == null)
                continue;

            Renderer renderer = mesh.GetComponent<Renderer>();
            int index = Array.IndexOf(originalRenderers, renderer);

            if (index < 0 || !rendererStates[index])
                continue;

            if (candidate != null && candidate != mesh.transform)
                return null;

            candidate = mesh.transform;
        }

        if (candidate == null)
            return null;

        for (int i = 0; i < originalColliders.Length; i++)
        {
            Collider col = originalColliders[i];

            if (col == null || !colliderStates[i] || col.isTrigger)
                continue;

            if (col.transform != candidate &&
                !col.transform.IsChildOf(candidate))
            {
                return null;
            }
        }

        return candidate;
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
                "階段のResult Prefabを設定してください：" + pattern.name,
                this);
            return null;
        }

        Vector3 parentScale = transform.lossyScale;

        if (preserveResultPrefabSize &&
            (Mathf.Abs(parentScale.x) < 0.00001f ||
             Mathf.Abs(parentScale.y) < 0.00001f ||
             Mathf.Abs(parentScale.z) < 0.00001f))
        {
            Debug.LogError("階段の親Scaleに0を設定しないでください。", this);
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

        root.transform.localPosition = pattern.resultLocalOffset;
        root.transform.localRotation =
            Quaternion.Euler(0f, pattern.resultRotationY, 0f);

        if (pattern.useResultRotationX)
        {
            Transform slope =
                string.IsNullOrWhiteSpace(pattern.resultRotationTargetPath)
                ? null
                : root.transform.Find(pattern.resultRotationTargetPath);

            if (slope == null)
            {
                Debug.LogError(
                    "Result Rotation Target Pathに傾斜部分の子のパスを設定してください。",
                    this);

                root.SetActive(false);
                Destroy(root);
                return null;
            }

            Vector3 angles = slope.localEulerAngles;
            angles.x = pattern.resultRotationX;
            slope.localRotation = Quaternion.Euler(angles);
        }

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
                : new Color(0.05f, 0.8f, 0.95f);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            tiles[i].SetPropertyBlock(block);
        }
    }

    public void ResetSelection()
    {
        selectionMask = 0;
        selectionOrder.Clear();
        replaceOnFirstCell = false;

        if (editing)
            RefreshGrid();
    }

    public void CancelEdit()
    {
        selectionMask = committedMask;
        selectionOrder.Clear();
        selectionOrder.AddRange(committedOrder);
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

        GUI.Label(
            new Rect(10, 80, 600, 25),
            "Order: " + string.Join(" > ", selectionOrder));

        for (int i = 0; i < 9; i++)
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
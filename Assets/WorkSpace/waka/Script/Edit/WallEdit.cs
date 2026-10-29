using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WallEdit : MonoBehaviour, INetworkBuildingEditTarget
{
    [Serializable]
    public class Pattern
    {
        public string name;

        [Tooltip("削除するマス。下段0,1,2 / 中段3,4,5 / 上段6,7,8")]
        public int[] removedCells;

        [Tooltip("形状専用Prefab。空欄なら残ったマスから自動生成")]
        public GameObject resultPrefab;
    }

    [Header("壁のローカル寸法")]
    public Vector3 localCenter = Vector3.zero;
    public float width = 1f;
    public float height = 1f;
    public float thickness = 1f;

    [Header("Material")]
    public Material wallMaterial;
    public Material gridMaterial;

    [Header("編集パターン")]
    public List<Pattern> patterns = new List<Pattern>
    {
        new Pattern
        {
            name = "Window",
            removedCells = new[] { 4 }
        },
        new Pattern
        {
            name = "Doorway",
            removedCells = new[] { 1, 4 }
        }
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

    private readonly Renderer[] tiles = new Renderer[9];
    private readonly bool[] visited = new bool[9];

    private int committedMask;
    private int selectionMask;
    private int editRevision;

    private bool strokeRemove;
    private bool editing;
    private float faceSign;
    private Camera editCamera;

    private void Awake()
    {
        originalRenderers =
            GetComponentsInChildren<Renderer>(true);

        originalColliders =
            GetComponentsInChildren<Collider>(true);

        rendererStates = new bool[originalRenderers.Length];
        colliderStates = new bool[originalColliders.Length];

        for (int i = 0; i < originalRenderers.Length; i++)
            rendererStates[i] = originalRenderers[i].enabled;

        for (int i = 0; i < originalColliders.Length; i++)
            colliderStates[i] = originalColliders[i].enabled;
    }

    public bool BeginEdit(Camera camera)
    {
        if (editing || camera == null)
            return false;

        if (width <= 0 || height <= 0 || thickness <= 0)
        {
            Debug.LogError(
                "[壁編集] Width・Height・Thicknessは0より大きくしてください。",
                this);
            return false;
        }

        NetworkBuildingEdit network =
            GetComponentInParent<NetworkBuildingEdit>();

        if (network == null || !network.Ready)
        {
            Debug.LogWarning(
                "[壁編集] 編集同期の初期化が完了していません。",
                this);
            return false;
        }

        if (gridMaterial == null)
        {
            Debug.LogError(
                "[壁編集] Grid Materialを設定してください。",
                this);
            return false;
        }

        editRevision = network.Revision;
        editing = true;
        editCamera = camera;
        selectionMask = committedMask;

        Array.Clear(visited, 0, visited.Length);

        faceSign =
            transform.InverseTransformPoint(camera.transform.position).z
            >= localCenter.z ? 1f : -1f;

        grid = new GameObject("Edit grid (visual only)");
        grid.transform.SetParent(transform, false);

        for (int i = 0; i < 9; i++)
        {
            GameObject tile =
                GameObject.CreatePrimitive(PrimitiveType.Cube);

            tile.name = "Cell " + i;
            tile.layer = 2;
            tile.transform.SetParent(grid.transform, false);

            tile.transform.localPosition =
                CellCenter(i)
                + Vector3.forward * faceSign
                * (thickness / 2f + 0.015f);

            tile.transform.localScale = new Vector3(
                width / 3f * 0.94f,
                height / 3f * 0.94f,
                0.005f);

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
            (index % 3 - 1) * width / 3f,
            (index / 3 - 1) * height / 3f,
            0f);
    }

    public void Paint(Ray ray, bool startStroke)
    {
        if (!editing)
            return;

        if (startStroke)
            Array.Clear(visited, 0, visited.Length);

        Vector3 origin =
            transform.InverseTransformPoint(ray.origin);

        Vector3 direction =
            transform.InverseTransformVector(ray.direction);

        if (Mathf.Abs(direction.z) < 0.00001f)
            return;

        float planeZ =
            localCenter.z
            + faceSign * (thickness / 2f + 0.015f);

        float t = (planeZ - origin.z) / direction.z;

        if (t < 0)
            return;

        Vector3 point =
            origin + direction * t - localCenter;

        if (Mathf.Abs(point.x) >= width / 2f ||
            Mathf.Abs(point.y) >= height / 2f)
        {
            return;
        }

        int x = Mathf.Clamp(
            Mathf.FloorToInt((point.x / width + 0.5f) * 3f),
            0, 2);

        int y = Mathf.Clamp(
            Mathf.FloorToInt((point.y / height + 0.5f) * 3f),
            0, 2);

        int cell = y * 3 + x;

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
            if (cell < 0 || cell > 8)
                return false;

            mask |= 1 << cell;
        }

        return mask > 0 && mask < 511;
    }

    private static string DescribeMask(int mask)
    {
        List<int> cells = new List<int>();

        for (int i = 0; i < 9; i++)
        {
            if ((mask & (1 << i)) != 0)
                cells.Add(i);
        }

        return cells.Count == 0
            ? "なし（元の壁）"
            : string.Join(", ", cells);
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
            reason = "完成壁のルートにNetworkBuildingEditが必要です。";
            Debug.LogWarning("[壁編集] " + reason, this);
            return false;
        }

        Debug.Log(
            $"[壁編集要求] 削除マス：{DescribeMask(selectionMask)}",
            this);

        bool requested = network.RequestEdit(
            this,
            player,
            editRevision,
            selectionMask,
            new int[0],
            out reason);

        if (!requested)
            Debug.LogWarning("[壁編集要求失敗] " + reason, this);

        return requested;
    }

    public bool CommitNetworkSelection(
        int mask,
        int[] order,
        Transform player,
        out string reason)
    {
        reason = "";

        if (mask < 0 || mask >= 511)
        {
            reason = "壁の選択データが不正です。";
            Debug.LogWarning("[壁編集拒否] " + reason, this);
            return false;
        }

        CancelEdit();

        selectionMask = mask;
        editing = true;

        bool accepted = CommitEditLocal(player, out reason);

        if (!accepted)
        {
            Debug.LogWarning(
                $"[壁編集拒否] 削除マス：{DescribeMask(mask)}\n{reason}",
                this);

            CancelEdit();
        }

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
            reason = "同期データがありません。";
            return false;
        }

        // サーバーで確定済みなので受信側はPlayer重なり判定をしない
        return CommitNetworkSelection(
            snapshot.mask,
            snapshot.order,
            null,
            out reason);
    }

    private bool CommitEditLocal(
        Transform player,
        out string reason)
    {
        reason = "";

        if (!editing)
        {
            reason = "編集処理が開始されていません。";
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
            if (patterns != null)
            {
                foreach (Pattern item in patterns)
                {
                    if (item == null)
                        continue;

                    if (!TryGetMask(item.removedCells, out int mask))
                        continue;

                    if (mask != selectionMask)
                        continue;

                    if (pattern != null)
                    {
                        reason =
                            $"「{pattern.name}」と「{item.name}」に"
                            + "同じ削除マスが登録されています。"
                            + "同じ組み合わせは1つだけにしてください。";
                        return false;
                    }

                    pattern = item;
                }
            }

            if (pattern == null)
            {
                reason =
                    $"削除マス [{DescribeMask(selectionMask)}] "
                    + "に一致するパターンがありません。"
                    + "実行中の壁のPatternsを確認してください。";
                return false;
            }
        }

        GameObject candidate = null;

        if (selectionMask != 0)
        {
            candidate = CreateResult(pattern, out reason);

            if (candidate == null)
                return false;
        }

        Physics.SyncTransforms();

        Collider[] candidateColliders = candidate != null
            ? candidate.GetComponentsInChildren<Collider>()
            : originalColliders;

        if (!CheckPlayerClearance(
            candidateColliders,
            candidate == null,
            player,
            out reason))
        {
            RemoveResult(candidate);
            return false;
        }

        RemoveResult(result);
        result = candidate;

        SetOriginal(selectionMask == 0);
        committedMask = selectionMask;

        Debug.Log(
            result != null
                ? $"[壁編集成功] 生成した形状：{result.name}"
                : "[壁編集成功] 元の壁に戻しました。",
            this);

        Finish();
        return true;
    }

    private GameObject CreateResult(
        Pattern pattern,
        out string reason)
    {
        reason = "";

        if (!NetworkBuildingEdit.ValidateResultPrefab(
            pattern.resultPrefab,
            this))
        {
            reason =
                $"「{pattern.name}」のResult Prefabが検証で拒否されました。"
                + "直前のConsoleメッセージを確認してください。";
            return null;
        }

        GameObject root;

        if (pattern.resultPrefab != null)
        {
            root = Instantiate(
                pattern.resultPrefab,
                transform,
                false);

            // 結果Prefabの親は壁と同じローカル座標系を使う
            // メッシュの回転・位置調整はPrefabの子で行う
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.SetActive(true);
        }
        else
        {
            root = new GameObject(pattern.name);
            root.transform.SetParent(transform, false);

            Material material = wallMaterial;

            if (material == null && originalRenderers.Length > 0)
                material = originalRenderers[0].sharedMaterial;

            for (int i = 0; i < 9; i++)
            {
                if ((selectionMask & (1 << i)) != 0)
                    continue;

                GameObject cell =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);

                cell.transform.SetParent(root.transform, false);
                cell.transform.localPosition = CellCenter(i);
                cell.transform.localScale = new Vector3(
                    width / 3f,
                    height / 3f,
                    thickness);

                if (material != null)
                    cell.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        foreach (Transform child in
            root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = gameObject.layer;
        }

        bool hasSolidCollider = false;

        foreach (Collider col in
            root.GetComponentsInChildren<Collider>())
        {
            if (!col.enabled || col.isTrigger)
                continue;

            if (col is MeshCollider mesh && mesh.sharedMesh == null)
                continue;

            hasSolidCollider = true;
            break;
        }

        if (!hasSolidCollider)
        {
            reason =
                $"「{root.name}」に使用可能なColliderがありません。"
                + "有効なColliderを付け、Is TriggerをOFFにしてください。";

            RemoveResult(root);
            return null;
        }

        return root;
    }

    // 外接する四角い範囲ではなくColliderの形状で判定する
    private bool CheckPlayerClearance(
        Collider[] colliders,
        bool original,
        Transform player,
        out string reason)
    {
        reason = "";

        if (player == null)
            return true;

        Collider[] bodies =
            player.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (col == null ||
                col.isTrigger ||
                !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (original && !colliderStates[i])
                continue;

            if (!original && !col.enabled)
                continue;

            bool enabledBefore = col.enabled;

            try
            {
                col.enabled = true;
                Physics.SyncTransforms();

                foreach (Collider body in bodies)
                {
                    if (body == null ||
                        body == col ||
                        !body.enabled ||
                        body.isTrigger ||
                        !body.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    // 壁自体をPlayerのColliderとして扱わない
                    if (body.transform.IsChildOf(transform))
                        continue;

                    bool colSupported = IsPenetrationShape(col);
                    bool bodySupported = IsPenetrationShape(body);

                    if (!colSupported && !bodySupported)
                    {
                        reason =
                            "重なり判定に対応していないColliderの組み合わせです。"
                            + $" 壁：{col.name}、Player：{body.name}。"
                            + "PlayerにはCapsuleColliderなどを使用してください。";
                        return false;
                    }

                    // 対応するColliderを第一引数にする
                    Collider first = colSupported ? col : body;
                    Collider second = colSupported ? body : col;

                    bool overlapping = Physics.ComputePenetration(
                        first,
                        first.transform.position,
                        first.transform.rotation,
                        second,
                        second.transform.position,
                        second.transform.rotation,
                        out Vector3 direction,
                        out float depth);

                    if (overlapping && depth > 0.001f)
                    {
                        reason =
                            "変更後の壁とPlayerが重なっています。"
                            + $" Collider：{col.name}、重なり量：{depth:F4}。"
                            + "少し離れて編集してください。";
                        return false;
                    }
                }
            }
            finally
            {
                col.enabled = enabledBefore;
            }
        }

        return true;
    }

    private static bool IsPenetrationShape(Collider col)
    {
        return col is BoxCollider ||
               col is SphereCollider ||
               col is CapsuleCollider ||
               (col is MeshCollider mesh &&
                mesh.convex &&
                mesh.sharedMesh != null);
    }

    private void RemoveResult(GameObject obj)
    {
        if (obj == null)
            return;

        obj.SetActive(false);
        Destroy(obj);
    }

    private void SetOriginal(bool visible)
    {
        for (int i = 0; i < originalRenderers.Length; i++)
        {
            if (originalRenderers[i] != null)
            {
                originalRenderers[i].enabled =
                    visible && rendererStates[i];
            }
        }

        for (int i = 0; i < originalColliders.Length; i++)
        {
            if (originalColliders[i] != null)
            {
                originalColliders[i].enabled =
                    visible && colliderStates[i];
            }
        }
    }

    private void RefreshGrid()
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < 9; i++)
        {
            if (tiles[i] == null)
                continue;

            Color color = (selectionMask & (1 << i)) != 0
                ? new Color(1f, 0.35f, 0.1f)
                : new Color(0.1f, 0.65f, 1f);

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
        if (!editing || editCamera == null)
            return;

        for (int i = 0; i < 9; i++)
        {
            Vector3 point = transform.TransformPoint(
                CellCenter(i)
                + Vector3.forward * faceSign
                * (thickness / 2f + 0.025f));

            Vector3 screen = editCamera.WorldToScreenPoint(point);

            if (screen.z <= 0)
                continue;

            GUI.Label(
                new Rect(
                    screen.x - 6,
                    Screen.height - screen.y - 10,
                    24,
                    24),
                i.ToString());
        }
    }
}
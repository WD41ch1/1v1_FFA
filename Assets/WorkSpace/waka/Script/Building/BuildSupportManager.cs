using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class BuildSupportManager : MonoBehaviour
{
    [Header("接続判定")]
    [Min(0.001f)]
    public float contactTolerance = 0.08f;

    [Header("再判定間隔")]
    [Min(0.1f)]
    public float recheckInterval = 0.5f;

    [Header("下から順番に崩れる間隔")]
    [Min(0.01f)]
    public float collapseInterval = 0.2f;

    private static BuildSupportManager instance;

    private readonly HashSet<BuildSupport> pieces =
        new HashSet<BuildSupport>();

    private readonly HashSet<string> reportedErrors =
        new HashSet<string>();

    private bool dirty;
    private float nextCheck;
    private float nextCollapseTime;

    public static void Register(BuildSupport support)
    {
        if (!Application.isPlaying ||
            support == null ||
            !support.isActiveAndEnabled)
            return;

        if (instance == null)
        {
            instance = new GameObject("BuildSupportManager")
                .AddComponent<BuildSupportManager>();
        }

        instance.pieces.Add(support);
        instance.dirty = true;
    }

    public static void Unregister(BuildSupport support)
    {
        if (instance == null)
            return;

        instance.pieces.Remove(support);
        instance.dirty = true;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError(
                "BuildSupportManagerが重複しています。",
                this);

            enabled = false;
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (Time.time < nextCollapseTime)
            return;

        if (!dirty && Time.time < nextCheck)
            return;

        dirty = false;
        nextCheck = Time.time + Mathf.Max(0.1f, recheckInterval);

        Recalculate();
    }

    private bool IsUsable(Collider col)
    {
        return col != null &&
               col.enabled &&
               col.gameObject.activeInHierarchy &&
               !col.isTrigger;
    }

    private bool IsSupportedShape(Collider col)
    {
        if (col is BoxCollider ||
            col is SphereCollider ||
            col is CapsuleCollider)
            return true;

        MeshCollider mesh = col as MeshCollider;

        return mesh != null &&
               mesh.convex &&
               mesh.sharedMesh != null;
    }

    private string GetPath(Transform target)
    {
        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private string DescribeCollider(Collider col)
    {
        if (col == null)
            return "参照切れ（Missing / None）";

        string result =
            GetPath(col.transform) +
            "\n種類=" + col.GetType().Name +
            ", Enabled=" + col.enabled +
            ", Active=" + col.gameObject.activeInHierarchy +
            ", IsTrigger=" + col.isTrigger;

        MeshCollider mesh = col as MeshCollider;

        if (mesh != null)
        {
            result +=
                ", Convex=" + mesh.convex +
                ", Mesh=" +
                (mesh.sharedMesh != null
                    ? mesh.sharedMesh.name
                    : "未設定");
        }

        return result;
    }

    private void Report(
        string reason,
        Object context,
        HashSet<string> currentErrors)
    {
        string key = context.GetInstanceID() + ":" + reason;
        currentErrors.Add(key);

        if (reportedErrors.Add(key))
        {
            Debug.LogError(
                "[崩壊診断] " + reason,
                context);
        }
    }

    private bool Validate(
        BuildSupport piece,
        HashSet<string> currentErrors)
    {
        bool valid = true;

        NetworkObject networkObject =
            piece.GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            Report(
                GetPath(piece.transform) +
                "\nネットワークSpawn済みです。" +
                "このローカル削除版では処理できません。",
                piece,
                currentErrors);

            valid = false;
        }

        if (piece.groundLayer.value == 0)
        {
            Report(
                GetPath(piece.transform) +
                "\nBuildSupportのGround LayerがNothingです。" +
                "\n生成元のBuildCone / BuildRampなどの" +
                "Ground Layerを確認してください。",
                piece,
                currentErrors);

            valid = false;
        }

        piece.RefreshColliders();

        bool hasUsableCollider = false;
        Collider[] colliders = piece.supportColliders;

        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (!IsUsable(col))
                    continue;

                hasUsableCollider = true;

                if (!IsSupportedShape(col))
                {
                    Report(
                        "非対応のColliderがあります。\n" +
                        DescribeCollider(col),
                        col,
                        currentErrors);

                    valid = false;
                }
            }
        }

        if (!hasUsableCollider)
        {
            string details = "";

            if (colliders != null)
            {
                foreach (Collider col in colliders)
                    details += "\n・" + DescribeCollider(col);
            }

            // キャッシュに入っていないColliderも確認できるように表示
            string actual = "";

            foreach (Collider col in
                     piece.GetComponentsInChildren<Collider>(true))
            {
                actual += "\n・" + DescribeCollider(col);
            }

            Report(
                GetPath(piece.transform) +
                "\nSupport Collidersに使用可能なColliderがありません。" +
                "\n【登録されているCollider】" +
                (details.Length > 0 ? details : "\nなし") +
                "\n【現在の子階層にあるCollider】" +
                (actual.Length > 0 ? actual : "\nなし"),
                piece,
                currentErrors);

            valid = false;
        }

        return valid;
    }

    private void Recalculate()
    {
        pieces.RemoveWhere(
            p => p == null || !p.isActiveAndEnabled);

        var nodes = new List<BuildSupport>(pieces);

        if (nodes.Count == 0)
        {
            reportedErrors.Clear();
            return;
        }

        Physics.SyncTransforms();

        var currentErrors = new HashSet<string>();
        bool allValid = true;

        // 最初の1件だけで止めず、問題をすべて表示する
        foreach (BuildSupport piece in nodes)
        {
            if (!Validate(piece, currentErrors))
                allValid = false;
        }

        reportedErrors.IntersectWith(currentErrors);

        // 不正な接続判定による誤削除を防ぐ
        if (!allValid)
            return;

        var neighbors = new List<int>[nodes.Count];
        var supported = new bool[nodes.Count];
        var queue = new Queue<int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            neighbors[i] = new List<int>();

            if (TouchesGround(nodes[i]))
            {
                supported[i] = true;
                queue.Enqueue(i);
            }
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (!AreConnected(nodes[i], nodes[j]))
                    continue;

                neighbors[i].Add(j);
                neighbors[j].Add(i);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            foreach (int next in neighbors[current])
            {
                if (supported[next])
                    continue;

                supported[next] = true;
                queue.Enqueue(next);
            }
        }

        CollapseBottomFirst(nodes, supported);
    }

    private void CollapseBottomFirst(
        List<BuildSupport> nodes,
        bool[] supported)
    {
        BuildSupport lowest = null;
        float lowestY = float.PositiveInfinity;

        for (int i = 0; i < nodes.Count; i++)
        {
            BuildSupport piece = nodes[i];

            if (supported[i] ||
                piece == null ||
                !piece.isActiveAndEnabled)
                continue;

            float bottomY = float.PositiveInfinity;

            foreach (Collider col in piece.supportColliders)
            {
                if (IsUsable(col))
                    bottomY = Mathf.Min(bottomY, col.bounds.min.y);
            }

            if (float.IsPositiveInfinity(bottomY))
                bottomY = piece.transform.position.y;

            if (lowest == null ||
                bottomY < lowestY ||
                (Mathf.Approximately(bottomY, lowestY) &&
                 piece.GetInstanceID() < lowest.GetInstanceID()))
            {
                lowest = piece;
                lowestY = bottomY;
            }
        }

        if (lowest == null)
            return;

        nextCollapseTime =
            Time.time + Mathf.Max(0.01f, collapseInterval);

        lowest.Collapse();
        dirty = true;
    }

    private bool TouchesGround(BuildSupport piece)
    {
        float tolerance = Mathf.Max(0.001f, contactTolerance);

        foreach (Collider shape in piece.supportColliders)
        {
            if (!IsUsable(shape))
                continue;

            Bounds bounds = shape.bounds;

            Collider[] grounds = Physics.OverlapBox(
                bounds.center,
                bounds.extents + Vector3.one * tolerance,
                Quaternion.identity,
                piece.groundLayer,
                QueryTriggerInteraction.Ignore);

            foreach (Collider ground in grounds)
            {
                if (ground.GetComponentInParent<BuildSupport>() != null)
                    continue;

                Vector3 direction;
                float depth;

                bool touching = Physics.ComputePenetration(
                    shape,
                    shape.transform.position + Vector3.down * tolerance,
                    shape.transform.rotation,
                    ground,
                    ground.transform.position,
                    ground.transform.rotation,
                    out direction,
                    out depth);

                if (touching && direction.y > 0.25f)
                    return true;
            }
        }

        return false;
    }

    private bool AreConnected(BuildSupport a, BuildSupport b)
    {
        float tolerance = Mathf.Max(0.001f, contactTolerance);

        foreach (Collider first in a.supportColliders)
        {
            foreach (Collider second in b.supportColliders)
            {
                if (!IsUsable(first) || !IsUsable(second))
                    continue;

                Bounds expanded = first.bounds;
                expanded.Expand(tolerance * 2f);

                if (!expanded.Intersects(second.bounds))
                    continue;

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            Vector3 offset =
                                new Vector3(x, y, z).normalized *
                                tolerance;

                            Vector3 direction;
                            float depth;

                            if (Physics.ComputePenetration(
                                first,
                                first.transform.position + offset,
                                first.transform.rotation,
                                second,
                                second.transform.position,
                                second.transform.rotation,
                                out direction,
                                out depth))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
}
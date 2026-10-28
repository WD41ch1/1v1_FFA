using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;

[DisallowMultipleComponent]
public class NetworkBuildingEdit : NetworkBehaviour
{
    [Header("編集可能な距離")]
    [Min(0.1f)]
    public float maxEditDistance = 8f;

    [Header("編集を遮る物体のLayer")]
    public LayerMask obstructionMask = ~0;

    [Header("編集の最短間隔")]
    [Min(0.01f)]
    public float minimumInterval = 0.1f;

    private readonly SyncVar<string> state =
        new SyncVar<string>();

    private INetworkBuildingEditTarget target;
    private MonoBehaviour targetComponent;

    private int revision;
    private int appliedRevision = -1;
    private float nextEditTime;

    private bool pending;
    private uint nextRequest;
    private uint pendingRequest;

    public int Revision =>
        IsServerInitialized ? revision : appliedRevision;

    public bool Ready =>
        IsClientInitialized &&
        targetComponent != null &&
        appliedRevision >= 0 &&
        !pending;

    /// <summary>
    /// 自分、または同じチームの建築を編集できる。
    /// </summary>
    public bool CanPlayerEdit(Transform player)
    {
        if (player == null)
            return false;

        NetworkObject playerObject =
            player.GetComponentInParent<NetworkObject>();

        NetworkBuildPiece piece =
            GetComponent<NetworkBuildPiece>();

        if (playerObject == null || piece == null)
            return false;

        NetworkPlayerTeam playerTeam =
            playerObject.GetComponent<NetworkPlayerTeam>();

        return piece.CanEdit(playerTeam);
    }

    private void Awake()
    {
        state.OnChange += StateChanged;
        ResolveTarget();
    }

    private void ResolveTarget()
    {
        target = null;
        targetComponent = null;

        int count = 0;

        foreach (MonoBehaviour component in
                 GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(component is INetworkBuildingEditTarget candidate))
                continue;

            if (component.GetComponentInParent<NetworkBuildingEdit>() != this)
                continue;

            target = candidate;
            targetComponent = component;
            count++;
        }

        if (count != 1)
        {
            target = null;
            targetComponent = null;

            Debug.LogError(
                "[編集同期] 建築1個につき、編集同期に対応した" +
                "Editコンポーネントが1個必要です。検出数=" + count,
                this);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        ResolveTarget();

        revision = 0;
        nextEditTime = 0f;

        if (targetComponent == null)
            return;

        BuildingEditState initial =
            target.CaptureNetworkState();

        initial.revision = revision;

        state.Value = JsonUtility.ToJson(initial);
        appliedRevision = revision;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsServerInitialized)
            ApplyState(state.Value);
    }

    private void StateChanged(
        string previous,
        string current,
        bool asServer)
    {
        if (asServer ||
            IsServerInitialized ||
            !IsClientInitialized)
        {
            return;
        }

        ApplyState(current);
    }

    private void ApplyState(string json)
    {
        if (string.IsNullOrEmpty(json) ||
            targetComponent == null)
        {
            return;
        }

        BuildingEditState snapshot =
            JsonUtility.FromJson<BuildingEditState>(json);

        if (snapshot == null ||
            snapshot.revision <= appliedRevision)
        {
            return;
        }

        if (!target.ApplyNetworkState(
                snapshot,
                out string reason))
        {
            Debug.LogError(
                "[編集同期] 形状を復元できません：" + reason,
                this);
            return;
        }

        appliedRevision = snapshot.revision;
        Physics.SyncTransforms();
    }

    public bool RequestEdit(
        INetworkBuildingEditTarget source,
        Transform player,
        int expectedRevision,
        int mask,
        int[] order,
        out string reason)
    {
        reason = "";

        if (!Ready || !ReferenceEquals(source, target))
        {
            reason =
                "編集同期が未初期化、または前の編集の応答待ちです。";
            return false;
        }

        NetworkObject playerObject = player != null
            ? player.GetComponentInParent<NetworkObject>()
            : null;

        if (playerObject == null ||
            !playerObject.IsOwner ||
            !playerObject.IsClientInitialized)
        {
            reason =
                "操作中のPlayerのNetworkObjectを確認してください。";
            return false;
        }

        if (!CanPlayerEdit(player))
        {
            reason =
                "自分または同じチームの建築だけ編集できます。";
            return false;
        }

        pending = true;
        pendingRequest = ++nextRequest;

        // ホストでも二重適用しないよう先にグリッドを閉じる
        source.CancelEdit();

        EditServerRpc(
            playerObject,
            expectedRevision,
            mask,
            order,
            pendingRequest);

        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void EditServerRpc(
        NetworkObject playerObject,
        int expectedRevision,
        int mask,
        int[] order,
        uint requestId,
        NetworkConnection sender = null)
    {
        if (sender == null)
            return;

        string reason = "";

        if (targetComponent == null ||
            !targetComponent.isActiveAndEnabled)
        {
            reason = "編集対象がありません。";
        }
        else if (playerObject == null ||
                 !playerObject.IsSpawned ||
                 playerObject.Owner != sender ||
                 playerObject.GetComponent<PlayerManager>() == null)
        {
            reason = "自分のPlayerから編集してください。";
        }
        else if (!CanPlayerEdit(playerObject.transform))
        {
            reason =
                "自分または同じチームの建築だけ編集できます。";
        }
        else if (expectedRevision != revision)
        {
            reason =
                "他の編集が先に確定しました。" +
                "Eキーで選択し直してください。";
        }
        else if (Time.time < nextEditTime)
        {
            reason = "編集間隔が短すぎます。";
        }
        else if (mask < 0 ||
                 mask > 511 ||
                 (order != null && order.Length > 9))
        {
            reason = "編集データが不正です。";
        }
        else if (!CanReach(playerObject.transform))
        {
            reason =
                "対象が遠すぎるか、別の物体に遮られています。";
        }

        if (reason.Length != 0)
        {
            ResultTargetRpc(
                sender,
                requestId,
                false,
                reason,
                state.Value);
            return;
        }

        nextEditTime =
            Time.time + Mathf.Max(0.01f, minimumInterval);

        if (!target.CommitNetworkSelection(
                mask,
                order,
                playerObject.transform,
                out reason))
        {
            ResultTargetRpc(
                sender,
                requestId,
                false,
                reason,
                state.Value);
            return;
        }

        BuildingEditState snapshot =
            target.CaptureNetworkState();

        snapshot.revision = ++revision;
        appliedRevision = revision;

        state.Value = JsonUtility.ToJson(snapshot);

        Physics.SyncTransforms();

        BuildSupport support =
            GetComponent<BuildSupport>();

        if (support != null)
        {
            support.RefreshColliders();
            BuildSupportManager.Register(support);
        }

        ResultTargetRpc(
            sender,
            requestId,
            true,
            "",
            state.Value);
    }

    private bool CanReach(Transform player)
    {
        Physics.SyncTransforms();

        Collider body = player.GetComponent<Collider>();

        Vector3 origin =
            body != null && body.enabled
                ? body.bounds.center
                : player.position + Vector3.up;

        Vector3 destination = transform.position;
        float nearest = float.PositiveInfinity;

        foreach (Collider col in
                 GetComponentsInChildren<Collider>())
        {
            if (!col.enabled || col.isTrigger)
                continue;

            Vector3 point = col.ClosestPoint(origin);
            float distance = (point - origin).sqrMagnitude;

            if (distance < nearest)
            {
                nearest = distance;
                destination = point;
            }
        }

        Vector3 delta = destination - origin;
        float length = delta.magnitude;

        if (float.IsPositiveInfinity(nearest) ||
            length > maxEditDistance)
        {
            return false;
        }

        if (length < 0.01f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            delta / length,
            Mathf.Max(0f, length - 0.01f),
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            if (hitTransform == player ||
                hitTransform.IsChildOf(player) ||
                hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    [TargetRpc]
    private void ResultTargetRpc(
        NetworkConnection connection,
        uint requestId,
        bool accepted,
        string reason,
        string snapshot)
    {
        if (pending && pendingRequest == requestId)
            pending = false;

        if (!IsServerInitialized)
            ApplyState(snapshot);

        if (!accepted)
        {
            Debug.LogWarning(
                "[編集同期] 編集できません：" + reason,
                this);
        }
    }

    public override void OnStopClient()
    {
        if (targetComponent != null)
            target.CancelEdit();

        pending = false;

        if (!IsServerInitialized)
            appliedRevision = -1;

        base.OnStopClient();
    }

    /// <summary>
    /// 編集後の形状Prefabを検証する。
    /// 拒否した場合は参照先とColliderの状態を表示する。
    /// </summary>
    public static bool ValidateResultPrefab(
        GameObject prefab,
        UnityEngine.Object context)
    {
        // 空欄なら編集スクリプト側で形状を自動生成する
        if (prefab == null)
            return true;

        string prefabInfo = prefab.name;

#if UNITY_EDITOR
        prefabInfo += "\n参照先：" +
            UnityEditor.AssetDatabase.GetAssetPath(prefab);
#endif

        if (prefab.GetComponentInChildren<NetworkObject>(true) != null ||
            prefab.GetComponentInChildren<NetworkBehaviour>(true) != null ||
            prefab.GetComponentInChildren<BuildSupport>(true) != null ||
            prefab.GetComponentInChildren<Rigidbody>(true) != null)
        {
            Debug.LogError(
                "[編集同期] Result Prefabは形状専用にしてください。\n" +
                "NetworkObject・NetworkBehaviour・BuildSupport・" +
                "Rigidbodyを含めないでください。\n" +
                prefabInfo,
                context);

            return false;
        }

        foreach (MonoBehaviour script in
                 prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script is IBuildingEditTarget)
            {
                Debug.LogError(
                    "[編集同期] Result PrefabにEditコンポーネントを" +
                    "含めないでください。\n" +
                    prefabInfo,
                    context);

                return false;
            }
        }

        bool valid = true;

        foreach (Collider col in
                 prefab.GetComponentsInChildren<Collider>(true))
        {
            if (!col.enabled || col.isTrigger)
                continue;

            // 生成時にはルートを有効化する。
            // ルートより下の非アクティブな階層だけを除外する。
            bool inactiveChild = false;
            Transform current = col.transform;

            while (current != null && current != prefab.transform)
            {
                if (!current.gameObject.activeSelf)
                {
                    inactiveChild = true;
                    break;
                }

                current = current.parent;
            }

            if (inactiveChild)
                continue;

            if (col is BoxCollider ||
                col is SphereCollider ||
                col is CapsuleCollider)
            {
                continue;
            }

            string colliderPath = GetColliderPath(
                prefab.transform,
                col.transform);

            if (col is MeshCollider mesh)
            {
                if (mesh.convex && mesh.sharedMesh != null)
                    continue;

                string meshName = mesh.sharedMesh != null
                    ? mesh.sharedMesh.name
                    : "None";

                Debug.LogError(
                    "[編集同期・Collider診断]\n" +
                    "Prefab：" + prefabInfo + "\n" +
                    "対象：" + colliderPath + "\n" +
                    "種類：MeshCollider\n" +
                    "Convex：" + mesh.convex + "\n" +
                    "Mesh：" + meshName + "\n" +
                    "原因：" +
                    (!mesh.convex ? "ConvexがOFFです。 " : "") +
                    (mesh.sharedMesh == null ? "Meshが未設定です。" : ""),
                    context);
            }
            else
            {
                Debug.LogError(
                    "[編集同期・Collider診断]\n" +
                    "Prefab：" + prefabInfo + "\n" +
                    "対象：" + colliderPath + "\n" +
                    "非対応の種類：" + col.GetType().Name,
                    context);
            }

            valid = false;
        }

        return valid;
    }

    private static string GetColliderPath(
        Transform root,
        Transform child)
    {
        string path = child.name;

        while (child != root && child.parent != null)
        {
            child = child.parent;
            path = child.name + "/" + path;
        }

        return path;
    }
}

// 以下の3つも、このファイル内に残してください。

[Serializable]
public class BuildingEditState
{
    public int revision;
    public int mask;
    public int[] order;

    public bool hasSlopePose;
    public int[] slopePath;
    public Vector3 slopePosition;
    public Quaternion slopeRotation;
}

public interface INetworkBuildingEditTarget : IBuildingEditTarget
{
    bool IsSelectionActive { get; }

    bool CommitNetworkSelection(
        int mask,
        int[] order,
        Transform player,
        out string reason);

    BuildingEditState CaptureNetworkState();

    bool ApplyNetworkState(
        BuildingEditState state,
        out string reason);
}

public static class BuildingEditTransformPath
{
    public static int[] Capture(
        Transform root,
        Transform child)
    {
        if (child == null ||
            (child != root && !child.IsChildOf(root)))
        {
            return null;
        }

        List<int> path = new List<int>();

        while (child != root)
        {
            path.Add(child.GetSiblingIndex());
            child = child.parent;
        }

        path.Reverse();
        return path.ToArray();
    }

    public static Transform Resolve(
        Transform root,
        int[] path)
    {
        if (path == null)
            return null;

        foreach (int index in path)
        {
            if (index < 0 || index >= root.childCount)
                return null;

            root = root.GetChild(index);
        }

        return root;
    }
}
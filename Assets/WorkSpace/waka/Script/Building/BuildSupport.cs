using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

[DisallowMultipleComponent]
public class BuildSupport : MonoBehaviour
{
    public LayerMask groundLayer;
    public Collider[] supportColliders;
    private Action onRemoved;
    private bool removedNotified;
    private bool collapsing;

    private bool CanManage()
    {
        NetworkObject obj = GetComponent<NetworkObject>();
        return obj != null && obj.IsSpawned && obj.IsServerInitialized;
    }

    public static void Attach(GameObject placed, LayerMask ground, Action removed)
    {
        if (placed == null) return;
        BuildSupport support = placed.GetComponent<BuildSupport>();
        if (support == null) support = placed.AddComponent<BuildSupport>();
        support.groundLayer = ground;
        support.onRemoved += removed;
        support.RefreshColliders();
        if (support.CanManage()) BuildSupportManager.Register(support);
    }

    private void Start()
    {
        RefreshColliders();
        if (CanManage()) BuildSupportManager.Register(this);
    }

    public void RefreshColliders()
    {
        var colliders = new List<Collider>();
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col == null || !col.enabled ||
                !col.gameObject.activeInHierarchy || col.isTrigger)
                continue;
            if (col.GetComponentInParent<BuildSupport>() != this) continue;
            colliders.Add(col);
        }
        supportColliders = colliders.ToArray();
    }

    private void OnEnable()
    {
        collapsing = false;
        if (CanManage())
        {
            RefreshColliders();
            BuildSupportManager.Register(this);
        }
    }

    private void OnDisable()
    {
        BuildSupportManager.Unregister(this);
    }

    private void OnDestroy()
    {
        BuildSupportManager.Unregister(this);
        if (!removedNotified)
        {
            removedNotified = true;
            onRemoved?.Invoke();
            onRemoved = null;
        }
    }

    public void Collapse()
    {
        if (collapsing || !CanManage()) return;
        NetworkBuildPiece piece = GetComponent<NetworkBuildPiece>();
        if (piece == null)
        {
            Debug.LogError("完成建築にNetworkBuildPieceが必要です。", this);
            return;
        }
        collapsing = true;
        BuildSupportManager.Unregister(this);
        piece.DestroyOnServer();
    }
}

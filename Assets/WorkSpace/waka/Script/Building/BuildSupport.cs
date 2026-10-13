using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildSupport : MonoBehaviour
{
    public LayerMask groundLayer;
    public Collider[] supportColliders;

    private Action onRemoved;

    // 建築スクリプトから呼ぶ
    public static void Attach(
        GameObject placed,
        LayerMask ground,
        Action removed)
    {
        BuildSupport support =
            placed.GetComponent<BuildSupport>();

        if (support == null)
            support = placed.AddComponent<BuildSupport>();

        support.groundLayer = ground;
        support.onRemoved += removed;
        support.RefreshColliders();

        BuildSupportManager.Register(support);
    }

    private void Start()
    {
        RefreshColliders();
        BuildSupportManager.Register(this);
    }

    public void RefreshColliders()
    {
        if (supportColliders != null &&
            supportColliders.Length > 0)
            return;

        var colliders = new List<Collider>();

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.enabled &&
                !col.isTrigger &&
                col.GetComponentInParent<BuildSupport>() == this)
            {
                colliders.Add(col);
            }
        }

        supportColliders = colliders.ToArray();
    }

    private void OnEnable()
    {
        if (supportColliders != null &&
            supportColliders.Length > 0)
        {
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

        // 建築済み位置の記録を消し、再建築を許可する
        onRemoved?.Invoke();
        onRemoved = null;
    }

    public void Collapse()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
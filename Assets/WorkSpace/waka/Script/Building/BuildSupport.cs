using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildSupport : MonoBehaviour
{
    [Header("本物の地面のLayer")]
    public LayerMask groundLayer;

    [Header("崩壊判定に使うCollider（自動更新）")]
    public Collider[] supportColliders;

    private Action onRemoved;

    /// <summary>
    /// 完成した建築を登録する
    /// </summary>
    public static void Attach(
        GameObject placed,
        LayerMask ground,
        Action removed)
    {
        if (placed == null)
            return;

        BuildSupport support =
            placed.GetComponent<BuildSupport>();

        if (support == null)
        {
            support =
                placed.AddComponent<BuildSupport>();
        }

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

    /// <summary>
    /// 編集後の形状も含めて、毎回Colliderを取得し直す
    /// </summary>
    public void RefreshColliders()
    {
        var colliders = new List<Collider>();

        foreach (Collider col in
                 GetComponentsInChildren<Collider>(true))
        {
            // 無効化された編集前のColliderは使わない
            if (col == null ||
                !col.enabled ||
                !col.gameObject.activeInHierarchy ||
                col.isTrigger)
            {
                continue;
            }

            // 別のBuildSupportが管理している子は除外する
            if (col.GetComponentInParent<BuildSupport>() != this)
                continue;

            colliders.Add(col);
        }

        supportColliders = colliders.ToArray();
    }

    private void OnEnable()
    {
        // 初回はStartまたはAttachで登録する
        if (supportColliders != null &&
            supportColliders.Length > 0)
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

        // 建築済み位置の記録を消し、再建築を許可する
        onRemoved?.Invoke();
        onRemoved = null;
    }

    /// <summary>
    /// 支えを失った建築を削除する
    /// ローカルInstantiateで生成された建築用
    /// </summary>
    public void Collapse()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Rigidbody rb;
    protected Transform shootPos;
    protected Vector3 direction;

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="data"></param>
    public abstract void Initialize(WeaponData data,Transform pos);

    /// <summary>
    /// 弾の挙動処理
    /// </summary>
    public abstract void AmmoBehavior();

    void Update()
    {
        AmmoBehavior();
    }
}

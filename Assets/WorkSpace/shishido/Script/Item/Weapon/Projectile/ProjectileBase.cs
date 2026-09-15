using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Rigidbody rb;
    protected Vector3 direction;
    protected Vector3 targetPosition;

    protected PlayerManager owner;
    protected WeaponBase wBase;
    protected WeaponData data;

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="data"></param>
    public abstract void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos);

    /// <summary>
    /// 弾の挙動処理
    /// </summary>
    public abstract void AmmoBehavior();

    public abstract DamageInfo CreateDamageInfo();

    void Update()
    {
        AmmoBehavior();
    }

    public void OnTriggerEnter(Collider other)
    {
        //  仮のヒット判定
        PlayerManager pm = other.gameObject.GetComponent<PlayerManager>();

        //  NULLもしくは射撃者なら
        if (pm == null || pm == owner) return;

        pm?.playerHealth.TakeDamage(CreateDamageInfo());
    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponBase : ItemBase
{
    protected WeaponData weaponData;

    protected WeaponView weaponView;

    //  現在の弾数
    protected int currentAmmo;

    public override void UsePrimary()
    {
        Fire();
    }

    public override void UseReload()
    {
        Reload();
    }

    /// <summary>
    /// 射撃処理
    /// </summary>
    public abstract void Fire();

    /// <summary>
    /// リロード処理
    /// </summary>
    public abstract void Reload();

    /// <summary>
    /// スコープを覗く(ADS)
    /// </summary>
    public virtual void ADS() { }
}

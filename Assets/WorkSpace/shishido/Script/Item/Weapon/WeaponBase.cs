using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponBase : ItemBase
{
    protected WeaponData weaponData;

    protected WeaponView weaponView;

    //  リロード中かいなか
    protected bool isReloading;
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

    /// <summary>
    /// リロードコルーチン
    /// </summary>
    /// <param name="reloadTime"></param>
    /// <returns></returns>
    protected IEnumerator ReloadAnimation(float reloadTime)
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);
        isReloading = false;
    }

    protected void BulletCreate(WeaponView weaponView)
    {
        if (weaponView == null) return;
        Transform muzzlePoint = weaponView.muzzlePoint;

        GameObject projectile =
            Instantiate(
                weaponView.projectilePrefab,
                muzzlePoint.position,
                muzzlePoint.rotation,
                transform.root.parent
                );

        projectile.GetComponent<ProjectileBase>()?
            .Initialize(weaponData, muzzlePoint);

        //  ※いずれプーリング処理で行う
        Destroy(projectile, 5.0f);
    }


}

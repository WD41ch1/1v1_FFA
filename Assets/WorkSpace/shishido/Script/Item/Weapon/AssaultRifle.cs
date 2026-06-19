using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssaultRifle : WeaponBase
{
    public override void Initialize(GameObject owner, ItemData itemData)
    {
        if (itemData is WeaponData)
        {
            weaponData = (WeaponData)itemData;
        }

        weaponView = GetComponent<WeaponView>();
        Reload();
    }

    public override void Fire()
    {
        //  残弾無し
        if(currentAmmo == 0)
        {
            Reload();
            return;
        }

        Debug.Log("発射");
        Debug.Log(weaponData.weaponDamage);

        BulletCreate();
    }

    private void BulletCreate()
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

    public override void Reload()
    {
        currentAmmo = weaponData.maxAmmo;
    }

    public override void ADS()
    {
        Debug.Log("ADS");
    }
}

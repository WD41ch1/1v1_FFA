using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class AssaultRifle : WeaponBase
{
    public override void Initialize(PlayerManager _owner, ItemData itemData)
    {
        if (itemData is WeaponData)
        {
            weaponData = (WeaponData)itemData;
        }

        //  使用者情報
        this.owner = _owner;

        weaponView = GetComponent<WeaponView>();

        currentAmmo = weaponData.maxAmmo;
    }

    public override void Fire()
    {
        //  リロード中
        if (isReloading)
            return;

        //  残弾無し
        if (currentAmmo == 0)
        {
            Reload();
            return;
        }

        Debug.Log("発射");
        Debug.Log(weaponData.weaponDamage);

        //  弾の生成(現状可視化するためのデバッグ用)
        BulletCreate(weaponView);

        //  弾数消費
        currentAmmo--;
    }

    public override void Reload()
    {
        //  すでにリロード中なら
        if (isReloading) return;

        //  補充必要数取得
        int requestValue = weaponData.maxAmmo - currentAmmo;
        //  承認
        int approvalValue = 0;
        //  inventory内弾数消費要求
        if (owner.inventoryManager.TryConsumeAmmo(
            weaponData.ammoType,
            requestValue,
            out approvalValue))
        {
            //  リロード時間
            StartCoroutine(ReloadAnimation(weaponData.reloadTime));

            //  弾補充     
            currentAmmo += approvalValue;
            //  TODO:現状武器を切り替え、落としてすぐに拾うなどをするとリロードしていないにもかかわらず弾が装填される

            //  万が一マガジン数が上限より上を行った場合
            if (weaponData.maxAmmo <= currentAmmo)
                currentAmmo = weaponData.maxAmmo;
        }

    }

    public override void ADS()
    {
        Debug.Log("ADS");
    }
}

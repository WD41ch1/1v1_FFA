using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmachineGun : WeaponBase
{
    public override void Initialize(
        PlayerManager _owner,
        ItemData itemData,
        WeaponState state = null)
    {
        if (itemData is WeaponData)
        {
            weaponData = (WeaponData)itemData;
        }

        //  使用者情報
        this.owner = _owner;

        weaponView = GetComponent<WeaponView>();

        weaponState = state;

    }

    public override void Fire()
    {
        //  リロード中
        if (isReloading)
            return;

        //  残弾無し
        if (weaponState.currentAmmo == 0)
        {
            Reload();
            return;
        }

        Debug.Log("発射");
        Debug.Log(weaponData.weaponDamage);

        //  弾の生成(現状可視化するためのデバッグ用)
        BulletShoot(weaponView, GetTarget());

        //  弾数消費
        weaponState.currentAmmo--;
    }

    public override void Reload()
    {
        //  すでにリロード中なら
        if (isReloading) return;

        //  補充必要数取得
        int requestValue = weaponData.maxAmmo - weaponState.currentAmmo;
        //  承認
        int approvalValue = 0;
        //  inventory内弾数消費要求
        if (owner.TryConsumeAmmo(
            weaponData.ammoType,
            requestValue,
            out approvalValue))
        {
            //  リロード時間
            StartCoroutine(ReloadAnimation(weaponData.reloadTime));

            //  弾補充     
            weaponState.currentAmmo += approvalValue;
            //  TODO:現状武器を切り替え、落としてすぐに拾うなどをするとリロードしていないにもかかわらず弾が装填される

            //  万が一マガジン数が上限より上を行った場合
            if (weaponData.maxAmmo <= weaponState.currentAmmo)
                weaponState.currentAmmo = weaponData.maxAmmo;
        }

    }
}

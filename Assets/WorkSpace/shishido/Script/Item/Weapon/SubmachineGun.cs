using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubmachineGun : WeaponBase
{
    public override bool Initialize(
        PlayerManager _owner,
        ItemData _itemData,
        ItemView _itemView,
        InventoryItem _itemState)
    {
        if (_owner == null || _itemData == null || _itemState == null)
            return false;

        weaponView = _itemView;

        if (weaponView == null)
            return false;

        if (_itemData is WeaponData)
        {
            SetSyncWeaponData(this, (WeaponData)_itemData);
            weaponData = syncWeaponData.Value;
        }
        else
        {
            // 失敗：WeaponDataではない、またはnull
            Debug.LogWarning("WeaponDataへの変換に失敗しました");
            return false;
        }

        //  使用者情報
        SetSyncOwner(this, _owner);

        itemState = _itemState;

        if (!CheckWeaponState(itemState, out weaponState))
            return false;

        return true;
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
        Debug.Log(syncWeaponData.Value.weaponDamage);

        Transform muzzlePoint = weaponView.muzzlePoint;

        //  弾の生成(現状可視化するためのデバッグ用)
        owner.BulletShootRequest(
            this,
            weaponView.projectilePrefab,
            muzzlePoint.position,
            muzzlePoint.rotation,
            transform.root.parent,
            GetTarget());

        //  弾数消費
        weaponState.currentAmmo--;

        //  UIの更新
        AmmoCheker();

    }

    public override void Reload()
    {
        //  すでにリロード中なら
        if (isReloading) return;

        //  補充必要数取得
        int requestValue = syncWeaponData.Value.magazineSize - weaponState.currentAmmo;
        //  承認
        int approvalValue = 0;
        //  inventory内弾数消費要求
        if (owner.TryConsumeAmmo(
            syncWeaponData.Value.ammoType,
            requestValue,
            out approvalValue))
        {
            //  リロード時間 + 弾補充
            StartCoroutine(ReloadAnimation(syncWeaponData.Value.reloadTime, approvalValue));

            //  万が一マガジン数が上限より上を行った場合
            if (syncWeaponData.Value.magazineSize <= weaponState.currentAmmo)
                weaponState.currentAmmo = syncWeaponData.Value.magazineSize;
        }

    }
}

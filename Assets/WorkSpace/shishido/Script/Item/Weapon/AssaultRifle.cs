using UnityEngine;

public class AssaultRifle : WeaponBase
{
    public override void Initialize(
        PlayerManager _owner,
        ItemData itemData,
        ItemState state = null)
    {
        if (itemData is WeaponData)
        {
            weaponData = (WeaponData)itemData;
        }

        //  使用者情報
        this.owner = _owner;

        weaponView = GetComponent<WeaponView>();

        weaponState = state;
        //weaponState.currentAmmo = weaponData.maxAmmo;

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

        //  UIの更新等の処理
        AmmoCheker();
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
            //  リロード時間 + 弾補充
            StartCoroutine(ReloadAnimation(weaponData.reloadTime, approvalValue));

            //  万が一マガジン数が上限より上を行った場合
            if (weaponData.maxAmmo <= weaponState.currentAmmo)
                weaponState.currentAmmo = weaponData.maxAmmo;
        }

    }

}

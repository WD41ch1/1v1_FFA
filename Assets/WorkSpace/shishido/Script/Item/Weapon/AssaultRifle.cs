using UnityEngine;

public class AssaultRifle : WeaponBase
{
    public override bool Initialize(
        PlayerManager _owner,
        ItemData _itemData,
        ItemView _itemView,
        InventoryItem _itemState)
    {
        if (_owner == null || _itemData == null || _itemState == null)
            return false;

        GameDebug.Server(
            $"[Initialize Server]" +
            $" Object:{gameObject.name}" +
            $" InstanceID:{GetInstanceID()}" +
            $" IsServer:{IsServerInitialized}" +
            $" IsClient:{IsClientInitialized}"
        );


        weaponView = _itemView;

        if (weaponView == null)
            return false;

        GameDebug.Log(
            $"[Initialize ItemData]" +
            $" Name:{_itemData?.name}" +
            $" Type:{_itemData?.GetType().FullName}" +
            $" IsWeaponData:{_itemData is WeaponData}"
        );

        //  使用者情報登録
        if (!InformationRegistr(_owner, _itemData, _itemState))
            return false;

        if (!CheckWeaponState(itemState, out weaponState))
            return false;

        return true;
    }

    public override void Fire()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        if (!owner.GetIsOwner())
            return;

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
        Debug.Log(data.weaponDamage);


        Transform muzzlePoint = weaponView.muzzlePoint;
        if (weaponView.projectilePrefab == null ||
            muzzlePoint == null)
        {
            Debug.Log("何かしらがNULL");
        }

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

        //  UIの更新等の処理
        AmmoCheker();
    }

    public override void Reload()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        //  すでにリロード中なら
        if (isReloading) return;

        //  補充必要数取得
        int requestValue = data.magazineSize - weaponState.currentAmmo;
        //  承認
        int approvalValue = 0;
        //  inventory内弾数消費要求
        if (owner.TryConsumeAmmo(
            data.ammoType,
            requestValue,
            out approvalValue))
        {
            //  リロード時間 + 弾補充
            StartCoroutine(ReloadAnimation(data.reloadTime, approvalValue));

            //  万が一マガジン数が上限より上を行った場合
            if (data.magazineSize <= weaponState.currentAmmo)
                weaponState.currentAmmo = data.magazineSize;
        }

    }

}

using FishNet.Object;
using System.Collections;
using UnityEngine;

public class ShotGun : WeaponBase
{
    //  拡散率
    private float spreadAngle;
    //  一回の射撃につき、発射される弾の数
    private int pelletCount = 10;
    //  撃ったか
    private bool isShooting;

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

        //  使用者情報登録
        if (!InformationRegistr(_owner, _itemData, _itemState))
            return false;

        if (!CheckWeaponState(itemState, out weaponState))
            return false;

        SetSpreadAngle();

        return true;
    }

    public override void Fire()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        //  リロード中
        if (isReloading)
            return;

        isShooting = true;

        //  残弾無し
        if (weaponState.currentAmmo == 0)
        {
            Reload();
            return;
        }

        Debug.Log("発射");
        Debug.Log(syncWeaponData.Value.weaponDamage);

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 target = GetTarget();

            Vector3 direction =
                (target - weaponView.muzzlePoint.position).normalized;

            Quaternion rotation =
                Quaternion.LookRotation(direction);

            //  ランダムに角度をつける
            rotation *= Quaternion.Euler(
                Random.Range(-spreadAngle, spreadAngle),
                Random.Range(-spreadAngle, spreadAngle),
                0f
            );

            Transform muzzlePoint = weaponView.muzzlePoint;

            owner.SGBulletShootRequest(
                this,
                weaponView.projectilePrefab,
                muzzlePoint.position,
                rotation
                );

        }

        //  弾数消費
        weaponState.currentAmmo--;

        isShooting = false;
    }

    /// <summary>
    /// リロード(1発ずつのリロード)
    /// </summary>
    public override void Reload()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        // すでにリロード中なら
        if (isReloading) return;

        // 満タンなら
        if (weaponState.currentAmmo >= data.magazineSize)
            return;

        StartCoroutine(ReloadAnimation(data.reloadTime, 0));
    }

    protected override IEnumerator ReloadAnimation(float reloadTime, int _approvalValue)
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            yield return null;

        isReloading = true;

        while (weaponState.currentAmmo < data.magazineSize)
        {
            // 射撃されたらリロード中断
            //if (isShooting)
            //    break;

            int approvalValue = 0;

            // 弾を1発消費
            if (!owner.TryConsumeAmmo(
                data.ammoType,
                1,
                out approvalValue))
            {
                // 弾切れ
                break;
            }

            // リロード時間
            yield return new WaitForSeconds(reloadTime);

            // マガジンに補充
            weaponState.currentAmmo += approvalValue;

            // 上限チェック
            if (weaponState.currentAmmo > data.magazineSize)
                weaponState.currentAmmo = data.magazineSize;
        }

        isReloading = false;
    }

    private void SetSpreadAngle()
    {
        if(!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        spreadAngle = data.fireAccuracy;
    }

}


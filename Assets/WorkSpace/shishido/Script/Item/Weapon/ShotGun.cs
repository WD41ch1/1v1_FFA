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
        InventoryItem _itemState)
    {
        if (_owner == null || _itemData == null || _itemState == null)
            return false;

        weaponView = GetComponent<WeaponView>();

        if (weaponView == null)
            return false;

        if (_itemData is WeaponData)
        {
            weaponData = (WeaponData)_itemData;
        }
        else
        {
            // 失敗：WeaponDataではない、またはnull
            Debug.LogWarning("WeaponDataへの変換に失敗しました");
            return false;
        }

        //  使用者情報
        this.owner = _owner;

        itemState = _itemState;

        if (!CheckWeaponState(itemState, out weaponState))
            return false;

        spreadAngle = weaponData.fireAccuracy;

        return true;
    }

    public override void Fire()
    {
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
        Debug.Log(weaponData.weaponDamage);

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
            GameObject projectile = Instantiate(
                weaponView.projectilePrefab,
                muzzlePoint.position,
                rotation
                );

            projectile.GetComponent<ProjectileBase>()?
            .Initialize(owner, this, Vector3.zero);

            Destroy(projectile, 5f);
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
        // すでにリロード中なら
        if (isReloading) return;

        // 満タンなら
        if (weaponState.currentAmmo >= weaponData.magazineSize)
            return;

        StartCoroutine(ReloadAnimation(weaponData.reloadTime, 0));
    }

    protected override IEnumerator ReloadAnimation(float reloadTime, int _approvalValue)
    {
        isReloading = true;

        while (weaponState.currentAmmo < weaponData.magazineSize)
        {
            // 射撃されたらリロード中断
            //if (isShooting)
            //    break;

            int approvalValue = 0;

            // 弾を1発消費
            if (!owner.TryConsumeAmmo(
                weaponData.ammoType,
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
            if (weaponState.currentAmmo > weaponData.magazineSize)
                weaponState.currentAmmo = weaponData.magazineSize;
        }

        isReloading = false;
    }
}


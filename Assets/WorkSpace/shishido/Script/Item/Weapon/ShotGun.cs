using GameKit.Dependencies.Utilities.ObjectPooling.Examples;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class ShotGun : WeaponBase
{
    //  拡散率
    private float spreadAngle;
    //  一回の射撃につき、発射される弾の数
    private int pelletCount = 10;
    //  撃ったか
    private bool isShooting;

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

        spreadAngle = weaponData.fireAccuracy;
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

            Destroy( projectile ,5f);
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
        if (weaponState.currentAmmo >= weaponData.maxAmmo)
            return;

        StartCoroutine(ReloadAnimation(weaponData.reloadTime,0));
    }

    protected override IEnumerator ReloadAnimation(float reloadTime, int _approvalValue)
    {
        isReloading = true;

        while (weaponState.currentAmmo < weaponData.maxAmmo)
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
            if (weaponState.currentAmmo > weaponData.maxAmmo)
                weaponState.currentAmmo = weaponData.maxAmmo;
        }

        isReloading = false;
    }
}


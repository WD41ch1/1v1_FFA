using DG.Tweening;
using FishNet.Example.ColliderRollbacks;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class WeaponBase : ItemBase
{
    protected WeaponData weaponData;

    protected WeaponView weaponView;

    //  リロード中かいなか
    protected bool isReloading;
    //  現在の弾数
    protected int currentAmmo;

    //  ADSができるかどうかのフラグ(例えば空中にいるときはADSできない など)
    protected bool canADS;

    public override void UsePrimary(InputAction.CallbackContext context)
    {
        if (context.started)
            StartFire();
        else if (context.canceled)
            StopFire();
    }

    public override void UseSecondary(bool flag)
    {
        ADS(flag);
    }

    public override void UseReload()
    {
        Reload();
    }

    /// <summary>
    /// 射撃処理
    /// </summary>
    public abstract void Fire(Vector3 _targetPosition);

    /// <summary>
    /// リロード処理
    /// </summary>
    public abstract void Reload();

    public void StartFire()
    {
        //  射撃
        Fire(GetTarget());

        InvokeRepeating(
            nameof(Fire),
            weaponData.fireRate,
            weaponData.fireRate
        );
    }

    public void StopFire()
    {
        CancelInvoke(nameof(Fire));
    }

    /// <summary>
    /// カメラで狙ってるものを取得
    /// </summary>
    /// <returns></returns>
    public Vector3 GetTarget()
    {
        RaycastHit target;
        Vector3 targetPosition;

        //  Ray(カメラ)を飛ばし当たったものを取得
        bool isRayHit = owner.cameraController.GetCrosshairTarget(out target);

        // 何かに当たった
        if (isRayHit)
        {
            //  発射位置
            Transform muzzlePoint = weaponView.muzzlePoint;

            //  軌道計算
            //targetPosition = (target.point - muzzlePoint.position).normalized;
            targetPosition = target.point;
        }
        // 何もない
        else
        {
            //  カメラの向いているほうを取得
            Ray ray = owner.cameraController.GetCrosshairRay();

            //  仮の目標地点を設定
            targetPosition = ray.origin + ray.direction * 1000f;
        }

        return targetPosition;
    }

    /// <summary>
    /// スコープを覗く(ADS)
    /// </summary>
    public void ADS(bool isADS)
    {
        if (isADS)
        {
            StartADS();
        }
        else
        {
            EndADS();
        }
    }


    public virtual void StartADS()
    {
        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(65f, 0.2f);

        camera.transform.DOMove(weaponView.adsPoint.position, 0.2f);

        camera.transform.DORotate(weaponView.adsPoint.eulerAngles, 0.2f);
    }

    public virtual void EndADS()
    {
        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(90f, 0.2f);

        //camera.transform.DOMove(normalPosition, 0.2f);

        //camera.transform.DORotate(normalRotation, 0.2f);
    }

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

    protected void BulletShoot(WeaponView weaponView, Vector3 target)
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
            .Initialize(owner, this, target);

        //  TODO: ※いずれプーリング処理で行う
        Destroy(projectile, 5.0f);
    }

    public WeaponData GetWeaponData()
    {
        return weaponData;
    }

    public int GetcurrentAmmo()
    {
        return currentAmmo;
    }
}

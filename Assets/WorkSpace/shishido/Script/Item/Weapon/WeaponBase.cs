using DG.Tweening;
using FishNet.Example.ColliderRollbacks;
using FishNet.Object;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using static GameConst;

public abstract class WeaponBase : ItemBase
{
    protected WeaponData weaponData;

    protected ItemView weaponView;

    protected WeaponState weaponState;

    //  リロード中かいなか
    protected bool isReloading;
    //  現在の弾数
    //protected int currentAmmo;
    private bool isFiring;


    //  ADSができるかどうかのフラグ(例えば空中にいるときはADSできない など)
    protected bool canADS;

    /// <summary>
    /// 射撃処理
    /// </summary>
    public abstract void Fire();

    /// <summary>
    /// リロード処理
    /// </summary>
    public abstract void Reload();


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

    protected void FireRequest()
    {
        if (owner == null)
            return;

        // 自分が操作しているプレイヤー以外は射撃要求を送らない
        if (!owner.IsOwner)
            return;

        OnFireRequest();
    }



    public void StartFire()
    {
        if (isFiring)
            return;

        isFiring = true;

        FireRequest();

        if (weaponData.fireMode == FireMode.FullAuto)
        {
            InvokeRepeating(
                nameof(FireRequest),
                weaponData.fireRate,
                weaponData.fireRate
            );
        }
    }

    public void StopFire()
    {
        isFiring = false;
        CancelInvoke(nameof(FireRequest));
    }

    public void OnFireRequest()
    {
        if (owner == null)
            return;


        if (IsServerInitialized)
        {
            FireInternal();
        }
        else if (IsClientInitialized)
        {
            FireServerRpc();
        }

    }

    [ServerRpc(RequireOwnership = false)]
    private void FireServerRpc()
    {
        FireInternal();
    }

    [Server]
    private void FireInternal()
    {
        Fire();
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

        camera.DOFieldOfView(weaponData.adsMultiplier, weaponData.adsSpeed);

        //camera.transform.DOMove(weaponView.adsPoint.position, 0.2f);
        //camera.transform.DORotate(weaponView.adsPoint.eulerAngles, 0.2f);
    }

    public virtual void EndADS()
    {
        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(DEFAULT_PLAYER_FOV, 0.2f);

        //camera.transform.DOMove(normalPosition, 0.2f);
        //camera.transform.DORotate(normalRotation, 0.2f);
    }


    protected void AmmoCheker()
    {
        if (weaponData == null) return;

        //  UIの更新要求
        owner.inventoryManager.SlotUpdateRequest(
            owner.equipmentManager.GetcurrentSlot(),
            weaponData.itemType
        );
    }
    /// <summary>
    /// リロードコルーチン
    /// </summary>
    /// <param name="reloadTime"></param>
    /// <param name="_approvalValue"></param>
    /// <returns></returns>
    protected virtual IEnumerator ReloadAnimation(float reloadTime, int _approvalValue)
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);

        //  弾補充     
        weaponState.currentAmmo += _approvalValue;

        //  HotBerSlotの更新
        AmmoCheker();

        isReloading = false;
    }

    protected virtual void BulletShoot(ItemView weaponView, Vector3 target)
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

    #region Getter

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


    public WeaponData GetWeaponData()
    {
        return weaponData;
    }

    public ItemView GetWeaponView()
    {
        return weaponView;
    }

    public int GetcurrentAmmo()
    {
        return weaponState.currentAmmo;
    }

    #endregion

    #region checker

    public bool CheckWeaponState(InventoryItem item, out WeaponState state)
    {
        state = null;

        if (item == null)
        {
            GameDebug.Warning("InventoryItem が null");
            return false;
        }

        if (item.state == null)
        {
            GameDebug.Warning("state が null");
            return false;
        }

        GameDebug.Log($"stateの実際の型: {item.state.GetType().FullName}");
        GameDebug.Log($"WeaponStateか: {item.state is WeaponState}");

        WeaponState weaponState = item.weaponState;

        if (weaponState == null)
        {
            GameDebug.Warning("WeaponStateとして取得できませんでした");
            return false;
        }

        state = weaponState;
        return true;
    }

    #endregion
}

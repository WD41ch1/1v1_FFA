using DG.Tweening;
using FishNet.Example.ColliderRollbacks;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;
using static GameConst;

public abstract class WeaponBase : ItemBase
{
    #region SerializeField
    [SerializeField]
    protected PlayerManager owner;

    [SerializeField]
    protected WeaponData weaponData;

    [SerializeField]
    protected ItemView weaponView;

    [SerializeField]
    protected WeaponState weaponState;
    #endregion

    #region SyncVar

    protected readonly SyncVar<WeaponData> syncWeaponData
        = new SyncVar<WeaponData>();

    #endregion

    //  リロード中かいなか
    protected bool isReloading;
    //  現在の弾数
    //protected int currentAmmo;
    private bool isFiring;


    //  ADSができるかどうかのフラグ(例えば空中にいるときはADSできない など)
    protected bool canADS;

    public readonly SyncVar<int> bulletShootCount = new();

    #region Server
    public override void OnStartServer()
    {
        base.OnStartServer();
        bulletShootCount.OnChange += OnchangeShootCount;
        syncOwner.OnChange += OnChangeOwner;
        syncWeaponData.OnChange += OnChangeWeaponData;
    }

    private void OnChangeOwner(PlayerManager prev, PlayerManager next, bool asServer)
    {
        GameDebug.Log($"[OnChangeOwner]{next},IsServer:{IsServerInitialized}");
    }

    private void OnChangeWeaponData(WeaponData prev, WeaponData next, bool asServer)
    {
        GameDebug.Log($"[OnChangeWeaponData]{next}");
    }

    private void OnchangeShootCount(int prev, int next, bool asServer)
    {
        GameDebug.Server($"[OnchangeShootCount]実行回数:{next}", false);
    }

    #endregion

    public void AddShootCount()
    {
        bulletShootCount.Value++;

        //  追加：射撃アニメーション通知
        NotifyShotAnimation();
    }

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
        owner = SetOwner();

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

        if (syncWeaponData.Value.fireMode == FireMode.FullAuto)
        {
            InvokeRepeating(
                nameof(FireRequest),
                syncWeaponData.Value.fireRate,
                syncWeaponData.Value.fireRate
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
        owner = SetOwner();

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
        owner = SetOwner();

        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(
            syncWeaponData.Value.adsMultiplier,
            syncWeaponData.Value.adsSpeed
            );

        //camera.transform.DOMove(weaponView.adsPoint.position, 0.2f);
        //camera.transform.DORotate(weaponView.adsPoint.eulerAngles, 0.2f);
    }

    public virtual void EndADS()
    {
        owner = SetOwner();

        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(DEFAULT_PLAYER_FOV, 0.2f);

        //camera.transform.DOMove(normalPosition, 0.2f);
        //camera.transform.DORotate(normalRotation, 0.2f);
    }


    protected void AmmoCheker()
    {
        if (syncWeaponData.Value == null) return;

        owner = SetOwner();

        //  UIの更新要求
        owner.inventoryManager.RequestSlotCounterUpdate(
            owner.equipmentManager.GetcurrentSlot(),
            syncWeaponData.Value.itemType
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

        //  追加：リロードアニメーション通知
        NotifyReloadAnimation();

        yield return new WaitForSeconds(reloadTime);

        //  弾補充     
        weaponState.currentAmmo += _approvalValue;

        //  HotBerSlotの更新
        AmmoCheker();

        isReloading = false;
    }

    #region Getter

    /// <summary>
    /// カメラで狙ってるものを取得
    /// </summary>
    /// <returns></returns>
    public Vector3 GetTarget()
    {
        owner = SetOwner();

        RaycastHit target;
        Vector3 targetPosition;

        //  Ray(カメラ)を飛ばし当たったものを取得
        bool isRayHit = owner.cameraController.GetCrosshairTarget(out target);

        // 何かに当たってかつ自身出なければ
        if (isRayHit && !CheckHitMe(target))
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
        return syncWeaponData.Value;
    }

    public ItemView GetWeaponView()
    {
        return weaponView;
    }

    public int GetcurrentAmmo()
    {
        return weaponState.currentAmmo;
    }

    protected PlayerManager SetOwner()
    {
        if (owner != null)
            return owner;
        else
            return syncOwner.Value;
    }
    protected WeaponData SetWeaponData()
    {
#if false
        if (weaponData != null)
            return weaponData;
        else
            return syncWeaponData.Value;
#endif
        return null;
    }


    [Server]
    public void SetSyncWeaponData(WeaponBase _script, WeaponData _data)
    {
        _script.syncWeaponData.Value = _data;
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

    private bool CheckHitMe(RaycastHit _target)
    {
        bool result = false;
        owner = SetOwner();
        PlayerManager targetManger =
            _target.collider.gameObject.GetComponentInParent<PlayerManager>();

        if (targetManger == owner)
            result = true;

        return result;
    }

    #endregion

    #region 追加：射撃・リロードのアニメーション通知

    //射撃通知
    public event Action ShotAnimationRequested;
    //リロード通知
    public event Action ReloadAnimationRequested;

    private WeaponUpperBodyController GetAnimationController()
    {
        owner = SetOwner();

        if (owner == null)
            return null;

        WeaponUpperBodyController controller =
            owner.GetComponent<WeaponUpperBodyController>();

        if (controller == null)
        {
            controller =
                owner.GetComponentInChildren<WeaponUpperBodyController>(true);
        }

        return controller;
    }

    private void NotifyShotAnimation()
    {
        ShotAnimationRequested?.Invoke();

        if (syncWeaponData.Value == null)
            return;

        WeaponUpperBodyController controller =
            GetAnimationController();

        if (controller != null)
        {
            controller.NotifyWeaponShot(
                syncWeaponData.Value.weaponAnimationType);
        }
    }


    private void NotifyReloadAnimation()
    {
        ReloadAnimationRequested?.Invoke();

        if (syncWeaponData.Value == null)
            return;

        WeaponUpperBodyController controller =
            GetAnimationController();

        if (controller != null)
        {
            controller.NotifyWeaponReload(
                syncWeaponData.Value.weaponAnimationType);
        }
    }

    #endregion
}
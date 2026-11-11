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

    public readonly SyncVar<int> bulletShootCount = new();

    private void Awake()
    {
        bulletShootCount.OnChange += OnchangeShootCount;
    }

    private void OnchangeShootCount(int prev, int next, bool asServer)
    {
        GameDebug.Server($"[OnchangeShootCount]実行回数:{next}");
    }

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

        camera.DOFieldOfView(
            weaponData.adsMultiplier,
            weaponData.adsSpeed);

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
    protected virtual IEnumerator ReloadAnimation(
        float reloadTime,
        int _approvalValue)
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
        RaycastHit target;
        Vector3 targetPosition;

        //  Ray(カメラ)を飛ばし当たったものを取得
        bool isRayHit =
            owner.cameraController.GetCrosshairTarget(out target);

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


    public bool CheckWeaponState(
        InventoryItem item,
        out WeaponState state)
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

    #region 追加：射撃・リロードのアニメーション通知

    //射撃通知
    public event Action ShotAnimationRequested;

    //リロード通知
    public event Action ReloadAnimationRequested;

    private WeaponUpperBodyController GetAnimationController()
    {
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

    // 構えと同じ、EquipmentManagerのID別設定を使う。
    private WeaponAnimationType GetWeaponAnimationType()
    {
        if (weaponData == null)
            return WeaponAnimationType.None;

        if (owner != null && owner.equipmentManager != null)
        {
            return owner.equipmentManager.GetAnimationType(weaponData);
        }

        return weaponData.weaponAnimationType;
    }

    private void NotifyShotAnimation()
    {
        ShotAnimationRequested?.Invoke();

        if (weaponData == null)
            return;

        WeaponUpperBodyController controller =
            GetAnimationController();

        if (controller == null)
        {
            Debug.LogWarning(
                "[武器アニメーション] 射撃の通知先が見つかりません。",
                this);
            return;
        }

        WeaponAnimationType type = GetWeaponAnimationType();

        if (type == WeaponAnimationType.None)
        {
            Debug.LogWarning(
                $"[武器アニメーション] 射撃：ID={weaponData.itemId}がNoneです。" +
                "PlayerのEquipmentManagerのID別設定を確認してください。",
                this);
            return;
        }

        // 追加：発射間隔も渡して、射撃アニメーションの速度を合わせる。
        controller.NotifyWeaponShot(type, weaponData.fireRate);
    }

    private void NotifyReloadAnimation()
    {
        ReloadAnimationRequested?.Invoke();

        if (weaponData == null)
            return;

        WeaponUpperBodyController controller =
            GetAnimationController();

        if (controller == null)
        {
            Debug.LogWarning(
                "[武器アニメーション] リロードの通知先が見つかりません。",
                this);
            return;
        }

        WeaponAnimationType type = GetWeaponAnimationType();

        if (type == WeaponAnimationType.None)
        {
            Debug.LogWarning(
                $"[武器アニメーション] リロード：ID={weaponData.itemId}がNoneです。" +
                "PlayerのEquipmentManagerのID別設定を確認してください。",
                this);
            return;
        }

        controller.NotifyWeaponReload(type);
    }

    #endregion
}
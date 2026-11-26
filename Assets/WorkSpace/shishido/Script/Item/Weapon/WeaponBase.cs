using DG.Tweening;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;

public abstract class WeaponBase : ItemBase
{
    #region SerializeField

    [SerializeField]
    protected PlayerManager myManager;

    [SerializeField]
    protected WeaponData weaponData;

    [SerializeField]
    protected ItemView weaponView;

    [SerializeField]
    protected WeaponState weaponState;

    #endregion

    #region SyncVar

    protected readonly SyncVar<WeaponData> syncWeaponData =
        new SyncVar<WeaponData>();

    public readonly SyncVar<int> bulletShootCount =
        new SyncVar<int>();

    #endregion

    protected bool isReloading;
    private bool isFiring;

    // 空中など、ADSを制限する場合に使用する。
    protected bool canADS;

    #region Server

    public override void OnStartServer()
    {
        base.OnStartServer();

        bulletShootCount.OnChange += OnchangeShootCount;
        syncOwner.OnChange += OnChangeOwner;
        syncWeaponData.OnChange += OnChangeWeaponData;
    }

    private void OnChangeOwner(
        PlayerManager prev,
        PlayerManager next,
        bool asServer)
    {
        GameDebug.Log(
            $"[OnChangeOwner]{next},IsServer:{IsServerInitialized}");
    }

    private void OnChangeWeaponData(
        WeaponData prev,
        WeaponData next,
        bool asServer)
    {
        GameDebug.Log(
            $"[OnChangeWeaponData] " +
            $"InstanceID:{GetInstanceID()} " +
            $"GameObject:{gameObject.name} " +
            $"IsServer:{IsServerInitialized} " +
            $"IsClient:{IsClientInitialized} " +
            $"asServer:{asServer} " +
            $"next:{next}",
            false);
    }

    private void OnchangeShootCount(
        int prev,
        int next,
        bool asServer)
    {
        GameDebug.Server(
            $"[OnchangeShootCount]実行回数:{next}",
            false);
    }

    #endregion

    /// <summary>
    /// 使用者情報登録。
    /// </summary>
    protected bool InformationRegistr(
        PlayerManager _owner,
        ItemData _itemData,
        InventoryItem _itemState)
    {
        myManager = _owner;
        SetSyncOwner(this, _owner);

        if (_itemData is WeaponData)
        {
            weaponData = (WeaponData)_itemData;
            SetSyncWeaponData(this, (WeaponData)_itemData);
        }
        else
        {
            if (!ItemDataManager.Instance.TryGetWeaponData(
                    _itemData.itemId,
                    out WeaponData resolvedData))
            {
                GameDebug.Warning("WeaponDataへの取得に失敗しました");
                return false;
            }

            weaponData = resolvedData;
            SetSyncWeaponData(this, resolvedData);
        }

        itemState = _itemState;
        SetSyncInventoryItem(this, _itemState);

        return true;
    }

    public void AddShootCount()
    {
        bulletShootCount.Value++;

        // 発射音は各武器の発射処理から通知する。
        // ここではアニメーションだけ通知する。
        NotifyShotAnimation();
    }

    public abstract void Fire();

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
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        if (!owner.IsOwner)
            return;

        OnFireRequest();
    }

    public void StartFire()
    {
        if (isFiring)
            return;

        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        if (!owner.IsClientInitialized || !owner.IsOwner)
            return;

        isFiring = true;

        FireRequest();

        if (data.fireMode == FireMode.FullAuto)
        {
            float interval = Mathf.Max(0.01f, data.fireRate);

            InvokeRepeating(
                nameof(FireRequest),
                interval,
                interval);
        }
    }

    /// <summary>
    /// 発射予約と、再生中の発射音を止める。
    /// </summary>
    public void StopFire()
    {
        isFiring = false;
        CancelInvoke(nameof(FireRequest));

        // ショットガン・ピストルは発射音を最後まで鳴らす。
        if (this is ShotGun || this is Pistol)
            return;

        // アサルト・SMGはボタンを離したら発射音を止める。
        StopWeaponFireSound();
    }

    public void OnFireRequest()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        Fire();
    }

    /// <summary>
    /// スコープを覗く。
    /// </summary>
    public void ADS(bool isADS)
    {
        if (isADS)
            StartADS();
        else
            EndADS();
    }

    public virtual void StartADS()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        Camera camera = owner.cameraController.PlayerCamera;

        if (camera == null)
            return;

        camera.DOFieldOfView(
            data.adsMultiplier,
            data.adsSpeed);
    }

    public virtual void EndADS()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        Camera camera = owner.cameraController.PlayerCamera;

        if (camera == null)
            return;

        camera.DOFieldOfView(DEFAULT_PLAYER_FOV, 0.2f);
    }

    protected void AmmoCheker()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        owner.inventoryManager.RequestSlotCounterUpdate(
            owner.equipmentManager.GetcurrentSlot(),
            data.itemType);
    }

    /// <summary>
    /// リロードコルーチン。
    /// </summary>
    protected virtual IEnumerator ReloadAnimation(
        float reloadTime,
        int _approvalValue)
    {
        if (isReloading || _approvalValue <= 0)
            yield break;

        isReloading = true;

        NotifyReloadAnimation();

        // リロード音は発射音の停止対象に含めない。
        PlayWeaponSound(GameSoundId.Reload);

        yield return new WaitForSeconds(reloadTime);

        weaponState.currentAmmo += _approvalValue;

        AmmoCheker();

        isReloading = false;
    }

    #region Getter

    public Vector3 GetTarget()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return Vector3.zero;
        }

        bool isRayHit =
            owner.cameraController.GetCrosshairTarget(
                out RaycastHit target);

        if (isRayHit && !CheckHitMe(target))
            return target.point;

        Ray ray = owner.cameraController.GetCrosshairRay();

        return ray.origin + ray.direction * 1000f;
    }

    public WeaponData GetWeaponData()
    {
        GameDebug.Log(
            $"[GetWeaponData] " +
            $"InstanceID:{GetInstanceID()} " +
            $"IsServer:{IsServerInitialized} " +
            $"IsClient:{IsClientInitialized} " +
            $"Data:{syncWeaponData.Value}");

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

    #endregion

    #region SyncVarSetter

    public void SetSyncWeaponData(
        WeaponBase _script,
        WeaponData _data)
    {
        if (IsServerInitialized)
            SetSyncWeaponDataServer(_script, _data);
        else
            SetSyncWeaponDataServerRpc(_script, _data);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetSyncWeaponDataServerRpc(
        WeaponBase _script,
        WeaponData _data)
    {
        SetSyncWeaponDataServer(_script, _data);
    }

    [Server]
    private void SetSyncWeaponDataServer(
        WeaponBase _script,
        WeaponData _data)
    {
        _script.syncWeaponData.Value = _data;
    }

    #endregion

    #region Checker

    protected bool TryGetWeaponResources(
        out WeaponData _data,
        out PlayerManager _myManager)
    {
        _data = null;
        _myManager = null;

        if (syncWeaponData.Value != null &&
            syncOwner.Value != null)
        {
            _data = syncWeaponData.Value;
            _myManager = syncOwner.Value;
            return true;
        }

        if (weaponData != null && myManager != null)
        {
            _data = weaponData;
            _myManager = myManager;
            return true;
        }

        return false;
    }

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

        if (item.weaponState == null)
        {
            GameDebug.Warning("weaponState が null");
            return false;
        }

        state = item.weaponState;
        return true;
    }

    private bool CheckHitMe(RaycastHit target)
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return false;
        }

        PlayerManager targetManager =
            target.collider.GetComponentInParent<PlayerManager>();

        return targetManager == owner;
    }

    #endregion

    #region 射撃・リロードのアニメーション通知

    public event Action ShotAnimationRequested;
    public event Action ReloadAnimationRequested;

    private WeaponUpperBodyController GetAnimationController()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return null;
        }

        WeaponUpperBodyController controller =
            owner.GetComponent<WeaponUpperBodyController>();

        if (controller == null)
        {
            controller =
                owner.GetComponentInChildren<WeaponUpperBodyController>(
                    true);
        }

        return controller;
    }

    // 構えと同じ、EquipmentManagerのID別設定を使う。
    private WeaponAnimationType GetWeaponAnimationType()
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return WeaponAnimationType.None;
        }

        if (owner.equipmentManager != null)
            return owner.equipmentManager.GetAnimationType(data);

        return data.weaponAnimationType;
    }

    private void NotifyShotAnimation()
    {
        ShotAnimationRequested?.Invoke();

        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

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
                $"[武器アニメーション] 射撃：ID={data.itemId}がNoneです。" +
                "PlayerのEquipmentManagerのID別設定を確認してください。",
                this);
            return;
        }

        controller.NotifyWeaponShot(type, data.fireRate);
    }

    private void NotifyReloadAnimation()
    {
        ReloadAnimationRequested?.Invoke();

        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

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
                $"[武器アニメーション] リロード：ID={data.itemId}がNoneです。" +
                "PlayerのEquipmentManagerのID別設定を確認してください。",
                this);
            return;
        }

        controller.NotifyWeaponReload(type);
    }

    #endregion

    #region 武器サウンド

    private bool warnedMissingWeaponSoundEmitter;

    private NetworkSoundEmitter GetLocalSoundEmitter()
    {
        PlayerManager owner =
            syncOwner.Value != null
                ? syncOwner.Value
                : myManager;

        if (owner == null ||
            !owner.IsClientInitialized ||
            !owner.IsOwner)
        {
            return null;
        }

        NetworkSoundEmitter emitter =
            owner.GetComponent<NetworkSoundEmitter>();

        if (emitter == null && !warnedMissingWeaponSoundEmitter)
        {
            warnedMissingWeaponSoundEmitter = true;

            Debug.LogWarning(
                "武器音：PlayerにNetworkSoundEmitterが必要です。",
                this);
        }

        return emitter;
    }

    protected void PlayWeaponSound(GameSoundId soundId)
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        NetworkSoundEmitter emitter = GetLocalSoundEmitter();

        if (emitter == null)
            return;

        emitter.RequestSound(
            soundId,
            owner.transform.position);
    }

    protected void NotifyWeaponFireSound()
    {
        GameSoundId soundId;

        if (this is ShotGun)
            soundId = GameSoundId.ShotgunFire;
        else if (this is Pistol)
            soundId = GameSoundId.PistolFire;
        else if (this is SubmachineGun)
            soundId = GameSoundId.SubmachineFire;
        else
            soundId = GameSoundId.AssaultFire;

        PlayWeaponSound(soundId);
    }

    private void StopWeaponFireSound()
    {
        NetworkSoundEmitter emitter = GetLocalSoundEmitter();

        if (emitter != null)
            emitter.StopGunfire();
    }

    public void PlayEquipSound()
    {
        PlayWeaponSound(GameSoundId.WeaponEquip);
    }

    #endregion
}
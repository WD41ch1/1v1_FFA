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
    protected PlayerManager myManager;

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
        GameDebug.Log(
               $"[OnChangeWeaponData] " +
               $"InstanceID:{GetInstanceID()} " +
               $"GameObject:{gameObject.name} " +
               $"IsServer:{IsServerInitialized} " +
               $"IsClient:{IsClientInitialized} " +
               $"asServer:{asServer} " +
               $"next:{next}"
           );
    }

    private void OnchangeShootCount(int prev, int next, bool asServer)
    {
        GameDebug.Server($"[OnchangeShootCount]実行回数:{next}", false);
    }

    #endregion

    /// <summary>
    /// 使用者情報登録
    /// </summary>
    /// <param name="_owner"></param>
    /// <param name="_itemData"></param>
    /// <param name="_itemState"></param>
    /// <returns></returns>
    protected bool InformationRegistr(PlayerManager _owner,
        ItemData _itemData,
        InventoryItem _itemState)
    {
        //  使用者情報
        myManager = _owner;
        SetSyncOwner(this, _owner);

        if (_itemData is WeaponData)
        {
            weaponData = (WeaponData)_itemData;
            SetSyncWeaponData(this, (WeaponData)_itemData);
        }
        else
        {
            bool success = false;
            if (!ItemDataManager.Instance.TryGetWeaponData(
                _itemData.itemId,
                out WeaponData _weaponData))
            {
                GameDebug.Warning("WeaponDataへの取得に失敗しました");
                success = false;
            }
            else
            {
                weaponData = _weaponData;
                SetSyncWeaponData(this, _weaponData);
                success = true;
            }

            // 失敗：WeaponDataではない、またはnull
            if (!success)
            {
                GameDebug.Warning("WeaponDataへの変換に失敗しました");
                return false;
            }
        }

        itemState = _itemState;
        SetSyncInventoryItem(this, _itemState);

        return true;
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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
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

        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        isFiring = true;

        FireRequest();

        if (data.fireMode == FireMode.FullAuto)
        {
            InvokeRepeating(
                nameof(FireRequest),
                data.fireRate,
                data.fireRate
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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(
            data.adsMultiplier,
            data.adsSpeed
            );

        //camera.transform.DOMove(weaponView.adsPoint.position, 0.2f);
        //camera.transform.DORotate(weaponView.adsPoint.eulerAngles, 0.2f);
    }

    public virtual void EndADS()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        Camera camera = owner.cameraController.PlayerCamera;
        if (camera == null) return;

        camera.DOFieldOfView(DEFAULT_PLAYER_FOV, 0.2f);

        //camera.transform.DOMove(normalPosition, 0.2f);
        //camera.transform.DORotate(normalRotation, 0.2f);
    }


    protected void AmmoCheker()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;

        //  UIの更新要求
        owner.inventoryManager.RequestSlotCounterUpdate(
            owner.equipmentManager.GetcurrentSlot(),
            data.itemType
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
        // 補充する弾がない場合は、音もアニメーションも開始しない。
        if (isReloading || _approvalValue <= 0)
            yield break;

        isReloading = true;

        NotifyReloadAnimation();

        // リロード開始時に1回だけ通知。
        PlayWeaponSound(GameSoundId.Reload);

        yield return new WaitForSeconds(reloadTime);

        weaponState.currentAmmo += _approvalValue;

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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return Vector3.zero;

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
        GameDebug.Log(
        $"[GetWeaponData] " +
        $"InstanceID:{GetInstanceID()} " +
        $"IsServer:{IsServerInitialized} " +
        $"IsClient:{IsClientInitialized} " +
        $"Data:{syncWeaponData.Value}"
        );

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

    #region SyncVarStter        

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_script"></param>
    /// <param name="_data"></param>
    public void SetSyncWeaponData(WeaponBase _script, WeaponData _data)
    {
        if (IsServerInitialized)
            SetSyncWeaponDataServer(_script, _data);
        else
            SetSyncWeaponDataServerRpc(_script, _data);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetSyncWeaponDataServerRpc(WeaponBase _script, WeaponData _data)
    {
        SetSyncWeaponDataServer(_script, _data);
    }
    [Server]
    private void SetSyncWeaponDataServer(WeaponBase _script, WeaponData _data)
    {
        _script.syncWeaponData.Value = _data;
    }

    #endregion

    #region checker

    protected bool TryGetWeaponResources(out WeaponData _data, out PlayerManager _myManager)
    {
        _data = null;
        _myManager = null;

        GameDebug.Log(
        $"[TryGetWeaponResources] " +
        $"InstanceID:{GetInstanceID()} " +
        $"GameObject:{gameObject.name}, " +
        $"IsServer:{IsServerInitialized}, " +
        $"IsClient:{IsClientInitialized}, " +
        $"syncWeaponData:{syncWeaponData.Value}, " +
        $"syncOwner:{syncOwner.Value}, " +
        $"weaponData:{weaponData}, " +
        $"myManager:{myManager}"
        );

        if (syncWeaponData.Value != null && syncOwner.Value != null)
        {
            _data = syncWeaponData.Value;
            _myManager = syncOwner.Value;
            return true;
        }
        else if (weaponData != null && myManager != null)
        {
            _data = weaponData;
            _myManager = myManager;
            return true;
        }

        return false;
    }

    public bool CheckWeaponState(InventoryItem item, out WeaponState state)
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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return false;

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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
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
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return WeaponAnimationType.None;            

        if (owner != null && owner.equipmentManager != null)
        {
            return owner.equipmentManager.GetAnimationType(data);
        }

        return data.weaponAnimationType;
    }

    private void NotifyShotAnimation()
    {
        ShotAnimationRequested?.Invoke();

        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
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
                $"[武器アニメーション] 射撃：ID={data.itemId}がNoneです。" +
                "PlayerのEquipmentManagerのID別設定を確認してください。",
                this);
            return;
        }

        // 追加：発射間隔も渡して、射撃アニメーションの速度を合わせる。
        controller.NotifyWeaponShot(type, data.fireRate);
    }

    private void NotifyReloadAnimation()
    {
        ReloadAnimationRequested?.Invoke();

        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
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

    protected void PlayWeaponSound(GameSoundId soundId)
    {
        if (!TryGetWeaponResources(
                out WeaponData data,
                out PlayerManager owner))
        {
            return;
        }

        // 現在の射撃・リロード操作は所有者側で実行される。
        // 所有者だけが通知し、重複再生を防ぐ。
        if (!owner.IsClientInitialized || !owner.IsOwner)
            return;

        NetworkSoundEmitter emitter =
            owner.GetComponent<NetworkSoundEmitter>();

        if (emitter == null)
        {
            if (!warnedMissingWeaponSoundEmitter)
            {
                warnedMissingWeaponSoundEmitter = true;

                Debug.LogWarning(
                    "武器音：PlayerにNetworkSoundEmitterが必要です。",
                    this);
            }

            return;
        }

        emitter.RequestSound(soundId, owner.transform.position);
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

    public void PlayEquipSound()
    {
        PlayWeaponSound(GameSoundId.WeaponEquip);
    }

    #endregion
}
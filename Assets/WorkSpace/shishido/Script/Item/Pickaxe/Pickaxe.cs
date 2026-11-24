using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pickaxe : WeaponBase
{
    [SerializeField]
    protected PlayerManager owner;

    private PickaxeData pickaxeData;

    private readonly float swingCooltime = 1f;
    private bool isSwingCooltime = false;

    // 同じ弾が建築物へ何度もダメージを与えないため
    private bool hasHitBuilding;

    public override bool Initialize(
        PlayerManager _owner,
        ItemData _itemData,
        ItemView _itemView,
        InventoryItem _itemState)
    {
        if (_owner == null || _itemData == null)
            return false;

        weaponView = _itemView;

        if (weaponView == null)
            return false;

        //  使用者情報登録
        if (!InformationRegistrPick(_owner, _itemData))
            return false;

        if (!CheckWeaponState(itemState, out weaponState))
            return false;

        return true;
    }

    #region ピッケル用メソッド


    private bool InformationRegistrPick(PlayerManager _owner,
        ItemData _itemData)
    {
        //  使用者情報
        myManager = _owner;
        SetSyncOwner(this, _owner);

        if (_itemData is PickaxeData)
        {
            pickaxeData = (PickaxeData)_itemData;
            SetSyncPickaxaData(this, (PickaxeData)_itemData);
        }
        else
        {
            bool success = false;
            if (!ItemDataManager.Instance.TryGetPickaxaData(
                _itemData.itemId,
                out PickaxeData _pickaxaData))
            {
                GameDebug.Warning("WeaponDataへの取得に失敗しました");
                success = false;
            }
            else
            {
                pickaxeData = _pickaxaData;
                SetSyncPickaxaData(this, _pickaxaData);
                success = true;
            }

            // 失敗：PickaxaDataではない、またはnull
            if (!success)
            {
                GameDebug.Warning("PickaxaDataへの変換に失敗しました");
                return false;
            }
        }
        return true;
    }

    protected bool TryGetPickeleResources(out PickaxeData _data, out PlayerManager _myManager)
    {
        _data = null;
        _myManager = null;

        GameDebug.Log(
        $"[TryGetWeaponResources] " +
        $"InstanceID:{GetInstanceID()} " +
        $"GameObject:{gameObject.name}, " +
        $"IsServer:{IsServerInitialized}, " +
        $"IsClient:{IsClientInitialized}, " +
        $"syncWeaponData:{syncPickaxaData.Value}, " +
        $"syncOwner:{syncOwner.Value}, " +
        $"pickaxeData:{pickaxeData}, " +
        $"myManager:{myManager}"
        );

        if (syncPickaxaData.Value != null && syncOwner.Value != null)
        {
            _data = syncPickaxaData.Value;
            _myManager = syncOwner.Value;
            return true;
        }
        else if (pickaxeData != null && myManager != null)
        {
            _data = pickaxeData;
            _myManager = myManager;
            return true;
        }

        return false;
    }


    #endregion

    public override void UsePrimary(InputAction.CallbackContext context)
    {
        if (!DoOnce.TryExecute(this))
            return;

        if (isSwingCooltime)
            return;

        SwingHit();
    }

    public override void Fire()
    {
        //  BaseFire内のTryGetWeaponResourcesが干渉してしまうためなし
        return;
    }

    public override void Reload()
    {
        //  補充する弾がないので特になし
        return;
    }


    private void SwingHit()
    {
        if (!TryGetPickeleResources(out PickaxeData data, out PlayerManager owner))
            return;

        isSwingCooltime = true;
        //  クールタイム開始
        StartSwingCookTime();

        RaycastHit target;

        //  Ray(カメラ)を飛ばし当たったものを取得
        bool isRayHit = owner.cameraController.GetCrosshairTarget(out target);

        //  当たったら
        if (isRayHit)
        {
            //  当たったらそれ以降処理しない
            if (BildHit(target))
            {
                return;
            }

            if (EnemyHit(target))
            {
                return;
            }
        }
    }

    /// <summary>
    /// スイングのクールタイム
    /// </summary>
    /// <returns></returns>
    private void StartSwingCookTime()
    {
        StartCoroutine(SwingCookTime(swingCooltime));
    }

    private IEnumerator SwingCookTime(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        isSwingCooltime = false;
        hasHitBuilding = false;
        DoOnce.Reset(this);
    }

    private bool BildHit(RaycastHit target)
    {
        if (!TryGetPickeleResources(out PickaxeData data, out PlayerManager owner))
            return false;

        //建築物へのダメージ処理
        // すでに建築物にヒットしていたら処理しない
        if (hasHitBuilding) return false;
        // 建築物のHPを持つコンポーネントを取得
        BuildHP buildHP = target.collider.GetComponentInParent<BuildHP>();
        // 建築物のHPが存在する場合、ダメージを与える
        if (buildHP != null)
        {
            // 建築物にヒットしたことを記録
            hasHitBuilding = true;
            // ダメージ前のHPを記録
            int beforeHP = buildHP.CurrentHP;
            // ダメージを与える
            buildHP.TakeDamage((int)data.weaponDamage);
            Debug.Log(
                $"建築命中：弾ID={GetInstanceID()} " +
                $"建築ID={buildHP.GetInstanceID()} " +
                $"HP {beforeHP} → {buildHP.CurrentHP} " +
                $"ダメージ={(int)data.weaponDamage}"
            );

            return true;
        }

        return false;
    }

    private bool EnemyHit(RaycastHit target)
    {
        //  仮のヒット判定
        PlayerManager targetPlayer
            = target.collider.GetComponentInParent<PlayerManager>();
        if (targetPlayer != null && targetPlayer != syncOwner.Value)
        {
            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.RequestTakeDamage(syncOwner.Value, targetPlayer, HitPart.Body, syncPickaxaData.Value);
                return true;
            }
        }

        return false;
    }
}

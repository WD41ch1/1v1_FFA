using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pickaxe : WeaponBase
{
    [SerializeField]
    protected PlayerManager owner;

    private PickaxeData pickaxeData;

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

    public override void Fire()
    {
        if (!TryGetWeaponResources(out WeaponData data, out PlayerManager owner))
            return;



    }

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

    public override void Reload()
    {
        //  補充する弾がないので特になし
        return;
    }


    private void Swing()
    {

    }
}

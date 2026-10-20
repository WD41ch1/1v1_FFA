using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pickaxe : ItemBase
{
    private PickaxeData pickaxeData;
    private PickaxeView pickaxeView;

    public override bool Initialize(PlayerManager _owner, ItemData _itemData, InventoryItem _itemState = null)
    {
        if (_owner == null || _itemData == null || _itemState == null)
            return false;

        pickaxeView = GetComponent<PickaxeView>();

        if (pickaxeView == null)
            return false;

        if (_itemData is PickaxeData)
        {
            pickaxeData = (PickaxeData)_itemData;
        }
        else
            return false;

        //  使用者情報
        this.owner = _owner;

        return true;
        
    }

    public override void UsePrimary(InputAction.CallbackContext context)
    {
        Swing();
    }

    private void Swing()
    {

    }
}

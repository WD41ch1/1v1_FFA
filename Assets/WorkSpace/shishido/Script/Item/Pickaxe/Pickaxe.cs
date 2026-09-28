using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Pickaxe : ItemBase
{
    private PickaxeData pickaxeData;
    private PickaxeView pickaxeView;

    public override void Initialize(
        PlayerManager _owner,
        ItemData itemData,
        ItemState state = null)
    {
        if (itemData is PickaxeData)
        {
            pickaxeData = (PickaxeData)itemData;
        }

        //  使用者情報
        this.owner = _owner;

        pickaxeView = GetComponent<PickaxeView>();

    }

    public override void UsePrimary(InputAction.CallbackContext context)
    {
        Swing();
    }

    private void Swing()
    {

    }
}

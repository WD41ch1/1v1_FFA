using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class HealingItemBase : ItemBase
{
    protected HealingItemData healingData;

    protected ItemState healingState;

    protected bool isHealing;

    public override void UsePrimary(InputAction.CallbackContext context)
    {
        if (healingState == null) return;

        Healing();
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    public abstract void Healing();

    /// <summary>
    /// 回復までにかかる処理
    /// </summary>
    /// <param name="reloadTime"></param>
    /// <returns></returns>
    protected abstract IEnumerator HealingAnimation(float reloadTime);

    /// <summary>
    /// スタック数の管理
    /// </summary>
    protected void StackCheker()
    {
        if (healingData == null) return;

        healingState.currentStack--;

        //  UIの更新要求
        owner.inventoryManager.SlotUpdateRequest(
            owner.equipmentManager.GetcurrentSlot(),
            healingData.itemType
        );

        //  インベントリから自身のアイテム情報を削除
        if (healingState.currentStack <= 0)
        {
            owner.inventoryManager.RemoveItem(healingState.useSlotNum);
        }

    }
}

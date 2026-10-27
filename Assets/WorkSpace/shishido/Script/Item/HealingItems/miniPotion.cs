using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class miniPotion : HealingItemBase
{
    public override bool Initialize(
        PlayerManager _owner, 
        ItemData _itemData,
        ItemView _itemView, 
        InventoryItem _itemState = null)
    {
        if (_owner == null || _itemData == null || _itemState == null)
            return false;

        if (_itemData is HealingItemData)
        {
            healingData = (HealingItemData)_itemData;
        }
        else
        {
            return false;
        }

        //  使用者情報
        this.owner = _owner;

        //itemState = _itemState;

        return true;    
    }

    /// <summary>
    /// 回復処理
    /// </summary>
    public override void Healing()
    {
        //  使用者がわからない もしくは すでに回復中　
        if (owner == null || isHealing ||
        //  もしくは アイテムの回復上限よりシールドが高かったら
            owner.playerHealth.GetcurrentShield() >= healingData.maxHealingAmount) return;

        StartCoroutine(HealingAnimation(healingData.healingTiem));

        owner.inventoryManager.SlotUpdateRequest(
            owner.equipmentManager.GetcurrentSlot(),
            healingData.itemType
            );

    }

    protected override IEnumerator HealingAnimation(float reloadTime)
    {
        isHealing = true;
        yield return new WaitForSeconds(reloadTime);

        //  使用者にシールド追加
        owner.playerHealth.AddShield(
            healingData.healingValue,
            healingData.maxHealingAmount
            );

        isHealing = false;

        //Debug.Log(
        //    $"使用Slot: {owner.equipmentManager.GetcurrentSlot()} " +
        //    $"StateSlot: {healingState.useSlotNum} " +
        //    $"Stack: {healingState.currentStack} " +
        //    $"StateID: {healingState.GetHashCode()}"
        //);

        StackCheker();
    }
}

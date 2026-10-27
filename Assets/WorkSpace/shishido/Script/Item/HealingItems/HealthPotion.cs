using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthPotion : HealingItemBase
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
        //  もしくは アイテムの回復上限よりHPが高かったら
            owner.playerHealth.GetcurrentHealth() >= healingData.maxHealingAmount) return;

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

        //  使用者にHP追加
        owner.playerHealth.RequestHeal(
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

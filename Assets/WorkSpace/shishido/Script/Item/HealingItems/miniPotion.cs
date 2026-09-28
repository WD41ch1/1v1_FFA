using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class miniPotion : HealingItemBase
{
    public override void Initialize(
        PlayerManager _owner,
        ItemData itemData,
        ItemState state = null)
    {
        if (itemData is HealingItemData)
        {
            healingData = (HealingItemData)itemData;
        }

        //  使用者情報
        this.owner = _owner;

        healingState = state;

    }

    /// <summary>
    /// 回復処理
    /// </summary>
    public override void Healing()
    {
        //  使用者がわからない もしくは すでに回復中　
        if (owner == null || isHealing ||
        //  もしくは アイテムの回復上限よりシールドが高かったら
            owner.playerHealth.currentShield >= healingData.maxHealingAmount) return;

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

        Debug.Log(
            $"使用Slot: {owner.equipmentManager.GetcurrentSlot()} " +
            $"StateSlot: {healingState.useSlotNum} " +
            $"Stack: {healingState.currentStack} " +
            $"StateID: {healingState.GetHashCode()}"
        );

        StackCheker();
    }
}

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    //  仮置きの所持上限
    public readonly int MaxAmmo = 999;
    public readonly int MaxMat = 999;


    //  アイテムスロット
    public List<ItemData> slots { get; private set; } = new();

    //  弾薬スロット
    public Dictionary<AmmoType, int> ammoDict =
        new Dictionary<AmmoType, int>();

    //  資材スロット
    public Dictionary<BildingMatType, int> bildMatDict
         = new Dictionary<BildingMatType, int>();

    #region 基本アイテム系(武器等)
    /// <summary>
    /// アイテムを拾う（追加)
    /// </summary>
    /// <param name="item"></param>
    public void AddItem(ItemData item)
    {
        if (slots.Count >= 5)
            return;

        slots.Add(item);

        Debug.Log(item.ItemName + "を取得");
    }

    /// <summary>
    /// アイテムを捨てる
    /// </summary>
    public void RemoveItem()
    {

    }

    public ItemData GetItem(int slotNumber)
    {
        if (slots == null || slots.Count == 0)
            return null;

        return slots[slotNumber];
    }

    #endregion

    #region 弾薬系

    public void AddAmmo(AmmoType type, int amount)
    {
        // 弾種が存在するか確認
        if (!ammoDict.ContainsKey(type))
            return;

        // 追加
        ammoDict[type] += amount;

        // 弾が上限に達していないか
        if (ammoDict[type] >= MaxAmmo)
        {
            int overValue = ammoDict[type] - MaxAmmo;
            ammoDict[type] = MaxAmmo;
            AmmoDroping(type, overValue);
        }
    }

    /// <summary>
    /// 捨てる処理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    public void AmmoDroping(AmmoType type, int amount)
    {

    }

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeAmmo(AmmoType type, int amount)
    {
        // 弾種が存在するか確認
        if (!ammoDict.TryGetValue(type, out int currentAmmo))
        {
            return false;
        }

        // 弾が足りるか確認
        if (currentAmmo < amount)
        {
            return false;
        }

        // 消費
        ammoDict[type] -= amount;

        return true;
    }

    #endregion

    #region 建材系

    public void AddBildMat(BildingMatType type, int amount)
    {
        // 弾種が存在するか確認
        if (!bildMatDict.ContainsKey(type))
            return;

        // 追加
        bildMatDict[type] += amount;

        // 建材が上限に達していないか
        if (bildMatDict[type] >= MaxMat)
        {
            int overValue = bildMatDict[type] - MaxMat;
            bildMatDict[type] = MaxMat;
            MatDroping(type, overValue);
        }
    }

    /// <summary>
    /// 捨てる処理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    public void MatDroping(BildingMatType type, int amount)
    {

    }


    /// <summary>
    /// 建材消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeBildMat(BildingMatType type, int amount)
    {
        // 弾種が存在するか確認
        if (!bildMatDict.TryGetValue(type, out int currentMat))
        {
            return false;
        }

        // 弾が足りるか確認
        if (currentMat < amount)
        {
            return false;
        }

        // 消費
        bildMatDict[type] -= amount;

        return true;
    }

    #endregion

}

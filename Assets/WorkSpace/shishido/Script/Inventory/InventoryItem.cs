
using System;
using UnityEngine;

[Serializable]
public class InventoryItem
{
    // アイテムの基本情報・固有設定
    public ItemData data;

    // アイテム個体の状態
    // WeaponState など ItemState の派生クラスを保持する
    [SerializeReference]
    public ItemState state;

    // この枠に入っている個数
    public int stackCount;

    //  スロット番号
    public int useSlotNum { get; private set; }

    public InventoryItem(ItemData itemData, int count = 1)
    {
        if (itemData == null)
            throw new ArgumentNullException(nameof(itemData));

        if (count < 1 || count > itemData.MaxStack)
            throw new ArgumentOutOfRangeException(nameof(count));

        data = itemData;
        stackCount = count;

        if (itemData is WeaponData weaponData)
        {
            state = new WeaponState(weaponData.magazineSize);
        }
        else
        {
            state = new ItemState();
        }
    }

    /// <summary>
    /// このアイテムをスタックできるか
    /// </summary>
    public bool CanStackWith(InventoryItem other)
    {
        if (other == null || data == null)
            return false;

        // 同じ種類のアイテムだけスタック可能
        // 武器は MaxStack = 1 のためスタック不可
        return data == other.data
            && data.MaxStack > 1
            && stackCount < data.MaxStack;
    }

    public void SetuseSlotNum(int num) { useSlotNum = num; }

    // WeaponStateを取得する
    public WeaponState weaponState => state as WeaponState;


}

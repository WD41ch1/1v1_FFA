using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    //  スロット
    public List<ItemData> slots = new();

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

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IPickupable
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public void Initialize(ItemData data)
    {
        itemData = data;
    }


    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddItem(itemData);
        Destroy(gameObject);
    }
}

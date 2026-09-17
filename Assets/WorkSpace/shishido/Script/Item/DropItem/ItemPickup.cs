using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IPickupable
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    public WeaponState weaponState;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public void Initialize(ItemData data, WeaponState state)
    {
        itemData = data;
        weaponState = state;
    }


    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddItem(itemData, weaponState);
        Destroy(gameObject);
    }
}

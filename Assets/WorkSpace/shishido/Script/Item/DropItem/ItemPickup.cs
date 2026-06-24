using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IPickupable
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    public void Pickup(TC_Character character)
    {
        character.inventoryManager.AddItem(itemData);
        Destroy(gameObject);
    }
}

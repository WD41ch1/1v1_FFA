using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour, IPickupable
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddItem(itemData);
        Destroy(gameObject);
    }
}

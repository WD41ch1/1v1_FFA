using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoPickup : MonoBehaviour, IPickupable
{
    [SerializeField]
    private AmmoType ammoType;

    [SerializeField]
    private int amount;


    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddAmmo(ammoType, amount);
        Destroy(gameObject);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialPickup : MonoBehaviour, IPickupable
{
    [SerializeField]
    private BildingMatType bildingMatType;

    [SerializeField]
    private int amount;


    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddBildMat(bildingMatType, amount);
        Destroy(gameObject);
    }
}
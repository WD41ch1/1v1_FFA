using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AmmoPickup : MonoBehaviour, IPickupable
{
    [SerializeField]
    private AmmoType ammoType;

    [SerializeField]
    private int amount;


    public void Pickup(TC_Character character)
    {

        Destroy(gameObject);
    }
}

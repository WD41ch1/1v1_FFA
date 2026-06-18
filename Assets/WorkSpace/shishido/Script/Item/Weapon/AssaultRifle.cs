using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssaultRifle : WeaponBase
{
    //public override void Initialize(ItemData itemData)
    //{
    //    if(itemData is WeaponData)
    //    {
    //        weaponData = (WeaponData)itemData;
    //    }
    //}

    public override void Fire()
    {

        Debug.Log("発射");
        Debug.Log(weaponData.weaponDamage);

        //Instantiate(
        //    weaponView.projectilePrefab,
        //    weaponView.muzzlePoint
        //    );
    }

    public override void Reload()
    {
        currentAmmo = weaponData.maxAmmo;
    }

    public override void ADS()
    {
        Debug.Log("ADS");
    }
}

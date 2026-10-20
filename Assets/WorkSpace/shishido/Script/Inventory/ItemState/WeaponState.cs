using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class WeaponState : ItemState
{
    // 現在マガジンに入っている弾数
    public int currentAmmo;

    public WeaponState(int initialAmmo)
    {
        currentAmmo = initialAmmo;
    }
}

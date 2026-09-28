using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class AmmoPickup : MonoBehaviour, IPickupable
{
    [SerializeField]
    private AmmoType ammoType;

    [SerializeField]
    private int amount;

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public void Initialize(AmmoType type,int _amount)
    {
        ammoType = type;
        amount = _amount;
    }

    /// <summary>
    /// 共通取得時関数
    /// </summary>
    /// <param name="character"></param>
    public void Pickup(PlayerManager character)
    {
        if (character.inventoryManager.GetAmmo(ammoType) >= MAX_AMMO)
            return;

        character.inventoryManager.AddAmmo(ammoType, amount);
        //Destroy(gameObject);
    }
}

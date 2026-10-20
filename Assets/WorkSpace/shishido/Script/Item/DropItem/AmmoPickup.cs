using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class AmmoPickup : PickupBase
{

    [SerializeField]
    private AmmoType ammoType;

    [SerializeField]
    private int amount;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public override void Initialize(AmmoType type, int _amount)
    {
        ammoType = type;
        amount = _amount;
    }

    /// <summary>
    /// 共通取得時関数
    /// </summary>
    /// <param name="character"></param>
    public override bool AddInventory(PlayerManager character)
    {
        if (character.inventoryManager.GetAmmo(ammoType) >= MAX_AMMO)
            return false;

        bool isAddAmmo = character.inventoryManager.AddAmmo(ammoType, amount);

        if (isAddAmmo)
        {
            GameDebug.Log(
               $"{character.name}が" +
               $"{amount}発の弾薬[{ammoType}]を取得"
            );

        }

        return isAddAmmo;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class MaterialPickup : MonoBehaviour, IPickupable
{
    [SerializeField]
    private BildingMatType bildingMatType;

    [SerializeField]
    private int amount;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public void Initialize(BildingMatType type, int _amount)
    {
        bildingMatType = type;
        amount = _amount;
    }

    public void Pickup(PlayerManager character)
    {
        if (character.inventoryManager.GetMat(bildingMatType) >= MAX_BILDMAT)
            return;

        character.inventoryManager.AddBildMat(bildingMatType, amount);
        //Destroy(gameObject);
    }
}
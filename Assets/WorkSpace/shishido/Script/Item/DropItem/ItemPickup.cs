using FishNet.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class ItemPickup : PickupBase
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    public ItemState itemState;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public override void Initialize(ItemData data, ItemState state)
    {
        itemData = data;
        itemState = state;

        SetAppearance();
    }

    public override void AddInventory(PlayerManager character)
    {
        character.inventoryManager.AddItem(itemData, itemState);
    }

    /// <summary>
    /// 見た目のせってい
    /// </summary>
    public override void SetAppearance()
    {
        if (itemData == null) return;

        //  見た目オブジェクトの生成
        Instantiate(itemData.AppearancePrefab, transform);
    }


}

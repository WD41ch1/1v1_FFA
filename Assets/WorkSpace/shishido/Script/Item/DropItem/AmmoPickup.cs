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

        SetAppearance();
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

    private int GetModelID()
    {
        int id = -1;

        switch (ammoType)
        {
            case AmmoType.SmallAmmo:
                id = MODEL_ID_SMALLAMMO;
                break;
            case AmmoType.MiddleAmmo:
                id = MODEL_ID_MIDDLEAMMO;
                break;
            case AmmoType.BigAmmo:
                id = MODEL_ID_BIGAMMO;
                break;
            case AmmoType.ShotgunAmmo:
                id = MODEL_ID_SGAMMO;
                break;
        }

        return id;
    }


    /// <summary>
    /// 見た目のせってい
    /// </summary>
    public override void SetAppearance()
    {

        int id = GetModelID();

        if (id == -1)
            return;

        //  すでに生成されているなら
        if (appearanceOBJ != null)
            Destroy(appearanceOBJ);

        AppearanceModelData modelData = null;

        if (!ItemDataManager.Instance.TryGetMatModelData(id, out modelData))
        {
            Despawn(this);
            return;
        }

        GameObject prefab = modelData.AppearancePrefab;
        if (prefab == null)
        {
            Despawn(this);
            return;
        }


        appearanceOBJ = Instantiate(prefab, transform);

        //if (appearanceOBJ == null) return;

        //Spawn(appearanceOBJ);

    }

}

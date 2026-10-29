using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class MaterialPickup : PickupBase
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
    public override void Initialize(BildingMatType type, int _amount)
    {
        bildingMatType = type;
        amount = _amount;

        SetAppearance();
    }

    public override bool AddInventory(PlayerManager character)
    {
        if (character.inventoryManager.GetMat(bildingMatType) >= MAX_BILDMAT)
            return false;

        bool isAddBildMat = 
            character.inventoryManager.AddBildMat(bildingMatType, amount);

        return isAddBildMat;
    }

    private int GetModelID()
    {
        int id = -1;

        switch (bildingMatType)
        {
            case BildingMatType.Wood:
                id = MODEL_ID_WOOD;
                break;
            case BildingMatType.Brick:
                id = MODEL_ID_BRICK;
                break;
            case BildingMatType.Iron:
                id = MODEL_ID_IRON;
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
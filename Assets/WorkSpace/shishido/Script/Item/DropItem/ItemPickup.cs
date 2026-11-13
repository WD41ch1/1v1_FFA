using FishNet.Object.Synchronizing;
using UnityEngine;
using static GameConst;

public class ItemPickup : PickupBase
{

    [Header("アイテムデータ")]
    public ItemData itemData;
    public ItemState itemState;

    private readonly SyncVar<ItemData> syncItemData = new SyncVar<ItemData>();
    private readonly SyncVar<ItemState> syncItemState = new SyncVar<ItemState>();


    public override void OnStartServer()
    {
        base.OnStartServer();

        if (itemData != null) syncItemData.Value = itemData;
        if (itemState != null) syncItemState.Value = itemState;
    }

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public override void Initialize(ItemData data, ItemState state)
    {
        itemData = data;
        itemState = state;

        if (itemData != null) syncItemData.Value = itemData;
        if (itemState != null) syncItemState.Value = itemState;

        SetAppearance();
    }


    public override bool AddInventory(PlayerManager character)
    {
        if (character == null)
            return false;


        bool success = SetAfterConfirmation_ItemData(out ItemData data);
        if (!success)
            return false;

        SetAfterConfirmation_ItemState(out ItemState state);

        return character.inventoryManager.AddItem(data, state);
    }



    /// <summary>
    /// 見た目のせってい
    /// </summary>
    public override void SetAppearance()
    {
        if (itemData == null) return;

        //  すでに生成されているなら
        if (appearanceOBJ != null)
            Destroy(appearanceOBJ);

#if true

        AppearanceModelData modelData = null;

        if (!ItemDataManager.Instance.TryGetModelData(itemData.itemId, out modelData))
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
#else

        //  見た目オブジェクトの生成
        appearanceOBJ = Instantiate(itemData.AppearancePrefab, transform);
#endif

    }

    private bool SetAfterConfirmation_ItemData(out ItemData _data)
    {
        bool result = false;
        _data = null;

        if (syncItemData.Value != null)
        {
            result = true;
            _data = syncItemData.Value;
        }
        else if (itemData != null)
        {
            result = true;
            _data = itemData;
        }

        return result;
    }
    private bool SetAfterConfirmation_ItemState(out ItemState _State)
    {
        bool result = false;
        _State = null;

        if (syncItemData.Value != null)
        {
            result = true;
            _State = syncItemState.Value;
        }
        else if (itemState != null)
        {
            result = true;
            _State = itemState;
        }

        return result;
    }
}

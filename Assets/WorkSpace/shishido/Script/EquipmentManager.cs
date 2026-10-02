using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine;
using static GameConst;
using static UnityEngine.UI.GridLayoutGroup;

/*
 現在何を装備しているか
 武器の生成
 武器の破棄
 使用処理の呼び出し
 を担当
 */
public class EquipmentManager : NetworkBehaviour
{
    private PlayerManager owner;

    [SerializeField, Header("アイテム生成場所")]
    private Transform itemSocket;
    //  現在装備スロット番号
    private readonly SyncVar<int> currentSlot = new SyncVar<int>();
    //  装備アイテム同期    TODO:
    private readonly SyncVar<int> currentItemID = new SyncVar<int>();
    //  現在装備しているもの
    private ItemBase currentItem;
    //  生成したアイテム
    private GameObject createItem;

    //  装備通知処理
    public event Action<int> OnEquipChanged;

    #region 初期化


    public void Initialize(PlayerManager _owner)
    {
        owner = _owner;

        owner.inventoryManager.OnDeletingSetItems += DeletingSetItems;
    }

    #endregion


    /// <summary>
    /// アイテムの装備
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="slotNum"></param>
    public void Equip(PlayerManager owner, int slotNum)
    {
        if (!IsOwner) return;

        ItemData data;
        //  装備したいアイテム情報を取得
        if (slotNum == PICKEL_SLOT)
            data = owner.inventoryManager.pickel;
        else
            data = owner.inventoryManager.GetItem(slotNum);

        if (data == null) return;

        //  指定しているスロット
        currentSlot.Value = slotNum;
        //  対応スロットのWeaponState取得(武器関連以外はNULLになる)
        ItemState state 
            = owner.inventoryManager.GetItemState(currentSlot.Value);
        //  UIに通知
        OnEquipChanged.Invoke(currentSlot.Value);
        //  見た目アイテムの生成
        CreateEquipItem(owner, data, state);
    }

    /// <summary>
    /// 装備アイテムの生成
    /// </summary>
    private void CreateEquipItem(PlayerManager owner, ItemData data, ItemState state)
    {
        //  生成場所があるか
        if (itemSocket == null || data == null)
            return;

        //  すでに何か装備していたら
        if (currentItem != null)
            UnEquip();

        //  アイテムの生成
        createItem = Instantiate(data.itemPrefab, itemSocket);
        //  生成したアイテムのItemBaseを取得
        currentItem = createItem.GetComponent<ItemBase>();
        //  装備アイテムの初期化
        currentItem.Initialize(owner, data, state);
    }

    /// <summary>
    /// アイテムの破棄
    /// </summary>
    public void UnEquip()
    {
        if (createItem == null) return;

        Destroy(createItem);
        createItem = null;
        currentItem = null;
    }

    /// <summary>
    /// inventoryManager用装備アイテム破棄関数
    /// </summary>
    public void DeletingSetItems()
    {
        UnEquip();
    }

    public ItemBase GetcurrentItem()
    {
        return currentItem;
    }
    public int GetcurrentSlot()
    {
        return currentSlot.Value;
    }

    public WeaponBase GetcurrentWeapon()
    {
        if(currentItem is WeaponBase weapon)
            return weapon;
        else
            return null;
    }

    /// <summary>
    /// 次のスロットアイテムを装備
    /// </summary>
    /// <param name="owner"></param>
    public void NextItemEquip(PlayerManager owner)
    {
        int index = currentSlot.Value;

        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            index = (index + 1) % ITEM_SLOT_MAX;

            if (owner.inventoryManager.GetItem(index) != null)
            {
                Equip(owner, index);
                return;
            }
        }
    }

    /// <summary>
    /// 前のスロットアイテムを装備
    /// </summary>
    /// <param name="owner"></param>
    public void PreviousItemEquip(PlayerManager owner)
    {
        int index = currentSlot.Value;

        for (int i = ITEM_SLOT_MAX; 0 <= i; i--)
        {
            index = (index - 1 + ITEM_SLOT_MAX) % ITEM_SLOT_MAX;

            if (owner.inventoryManager.GetItem(index) != null)
            {
                Equip(owner, index);
                return;
            }
        }
    }
}

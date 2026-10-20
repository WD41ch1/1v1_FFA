using FishNet;
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
    private PlayerManager myPlayer;

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

    public void RegisterPlayer(PlayerManager _myPlayer)
    {
        myPlayer = _myPlayer;
    }


    public void Initialize()
    {
        myPlayer.inventoryManager.OnDeletingSetItems += DeletingSetItems;
    }

    #endregion

    #region override

    public override void OnStartClient()
    {
        base.OnStartClient();

        currentSlot.OnChange += OnChangeEquipItem;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();

        currentSlot.OnChange -= OnChangeEquipItem;
    }

    public void OnChangeEquipItem(
        int prev,
        int next,
        bool asServer
        )
    {
        //  UIに通知
        OnEquipChanged.Invoke(next);
    }

    #endregion


    /// <summary>
    /// アイテムの装備
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="slotNum"></param>
    public bool Equip(int slotNum)
    {
        if (!IsOwner) return false;

        if (myPlayer == null) return false;

        ItemData data;
        //  装備したいアイテム情報を取得
        if (slotNum == PICKEL_SLOT)
            data = myPlayer.inventoryManager.pickel;
        else
            data = myPlayer.inventoryManager.GetItem(slotNum);

        if (data == null) return false;

        //  対応スロットのWeaponState取得(武器関連以外はNULLになる)
        InventoryItem state
            = myPlayer.inventoryManager.GetInvetoryItem(slotNum);

        if (state == null) return false;

        //  見た目アイテムの生成
        if (!CreateEquipItem(myPlayer, data, state))
            return false;

        //  指定しているスロット
        currentSlot.Value = slotNum;

        return true;
    }

    /// <summary>
    /// 装備アイテムの生成
    /// </summary>
    private bool CreateEquipItem(PlayerManager owner, ItemData data, InventoryItem state)
    {
        //  生成場所があるか
        if (itemSocket == null || data == null || state == null)
            return false;

        //  すでに何か装備していたら
        if (currentItem != null)
            UnEquip();

        //  アイテムの生成
        createItem = Instantiate(data.wearPrefab, itemSocket);

        //  アイテムが生成できなかった
        if(createItem == null)
            return false;

        //  生成したアイテムのItemBaseを取得
        currentItem = createItem.GetComponent<ItemBase>();

        if(currentItem == null)
        {
            GameDebug.Warning("currentItemが取得できません");
            return false;
        }

        //  装備アイテムの初期化
        bool success
            = currentItem.Initialize(owner, data, state);

        GameDebug.Log($"アイテム生成初期：[{success}]");

        return success;
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
        if (currentItem is WeaponBase weapon)
            return weapon;
        else
            return null;
    }

    /// <summary>
    /// 次のスロットアイテムを装備
    /// </summary>
    /// <param name="owner"></param>
    public void NextItemEquip()
    {
        if (myPlayer == null) return;

        int index = currentSlot.Value;

        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            index = (index + 1) % ITEM_SLOT_MAX;

            if (myPlayer.inventoryManager.GetItem(index) != null)
            {
                Equip(index);
                return;
            }
        }
    }

    /// <summary>
    /// 前のスロットアイテムを装備
    /// </summary>
    /// <param name="owner"></param>
    public void PreviousItemEquip()
    {
        if (myPlayer == null) return;

        int index = currentSlot.Value;

        for (int i = ITEM_SLOT_MAX; 0 <= i; i--)
        {
            index = (index - 1 + ITEM_SLOT_MAX) % ITEM_SLOT_MAX;

            if (myPlayer.inventoryManager.GetItem(index) != null)
            {
                Equip(index);
                return;
            }
        }
    }
}

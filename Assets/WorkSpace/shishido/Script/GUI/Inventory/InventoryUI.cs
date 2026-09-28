using DG.Tweening;
using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal.VersionControl;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEditor.Progress;
using static GameConst;

public class InventoryUI : UIBase, IDropHandler
{

    public InventoryManager im { private set; get; }

    //  表示切り替え変数
    private Vector2 showPos = new Vector2(-10, -30);
    private Vector2 hidePos = new Vector2(400, -30);
    private float toggleTime = 0.2f;

    [SerializeField]
    private List<InventoryUI_ItemDrop> ItemHolderList;

    //  アイコンの配置場所
    [SerializeField] private Transform BildMatListTrans;
    [SerializeField] private Transform ammoListTrans;
    [SerializeField] private Transform ItemListTrans;
    [SerializeField] private List<Transform> ItemHoldersTrams;

    //  プレファブ
    [SerializeField, Header("アイテムアイコンPrefab")]
    private GameObject ItemListPrefab;
    [SerializeField, Header("弾アイコンPrefab")]
    private GameObject AmmoListPrefab;
    [SerializeField, Header("建材アイコンPrefab")]
    private GameObject BIldMatListPrefab;

    [SerializeField,Header("items")]
    private List<IS_Item> items = new List<IS_Item>();

    private List<IS_Ammo> ammoList = new List<IS_Ammo>();
    private List<IS_BIldMat> BIldMatList = new List<IS_BIldMat>();
    protected override void OnInitialize()
    {
        im = myPlayer.inventoryManager;
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        // Image が None の場合の対応
        Image image = GetComponent<Image>();
        if (image != null)
        {
            // Color を透明にする
            image.color = new Color(1, 1, 1, 0);

            // Raycast Target は ON にしておく
            image.raycastTarget = true;
        }

        int holderIndex = 0;
        //  各ItemHolderの初期化
        foreach (InventoryUI_ItemDrop holder in ItemHolderList)
        {
            //  初期化
            holder.Initialize(myPlayer, gui);

            //  その他設定
            holder.Setting(this, holderIndex);
            holderIndex++;
        }
        //  transformの割り当て
        SetHolderTrans();

        //  UIアイテムスロットを全て空で登録
        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            items.Add(null);
        }

        //  イベント登録
        im.OnChangeInventoryItem += UpdateItem;
        im.OnChangeAmmo += UpdateAmmo;
        im.OnChangeBildMat += UpdateBIldMat;
    }

    void Update()
    {

    }

    /// <summary>
    /// transformの割り当て
    /// </summary>
    private void SetHolderTrans()
    {
        if (ItemHolderList == null) return;

        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            ItemHoldersTrams.Add(ItemHolderList[i].transform);
        }
    }

    /// <summary>
    /// アイテムホルダーのTransformGetter
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    public Transform GetHolderTrans(int number)
    {
        return ItemHoldersTrams[number];
    }

    #region アイテム

    /// <summary>
    /// アイテム更新
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <param name="changeType"></param>
    private void UpdateItem(int slotNum, ItemData data, ResourceChangeType changeType)
    {
        switch (changeType)
        {
            case ResourceChangeType.AddedNew:
                InitializeItemSlot(slotNum, data, ItemListPrefab, AddedNewItem(slotNum), items);
                break;
            case ResourceChangeType.Updated:
                UpdateSlot(items, data.itemType, 0);
                break;
            case ResourceChangeType.Removed:
                RemoveItemSlot(slotNum, data, items);
                break;
        }
    }

    private Transform AddedNewItem(int slotNum)
    {
        if (ItemHoldersTrams == null) return null;

        Transform ItemHolder = null;

        for (int i = 0; i <= ItemHoldersTrams.Count; i++)
        {
            if (i == slotNum)
            {
                ItemHolder = ItemHoldersTrams[i];
                break;
            }
        }

        return ItemHolder;
    }

    /// <summary>
    /// インベントリ内のアイテムスロット生成
    /// </summary>
    /// <param name="slotNum"></param>
    /// <param name="prefab"></param>
    /// <param name="trans"></param>
    /// <param name="data"></param>
    /// <param name="list"></param>
    private void InitializeItemSlot(
        int slotNum,
        ItemData data,
        GameObject prefab,
        Transform trans,
        List<IS_Item> list
       )
    {
        //  スロットの番号を内部インベントリの番号に登録
        IS_Item slot = list[slotNum];

        //  slotに何も入っていないなら
        if (slot == null)
        {
            //  生成
            GameObject ob = Instantiate(prefab, trans);
            //  特定のジェネリック型を持つInventorySlotBaseを取得
            slot = ob.GetComponent<IS_Item>();
        }

        //  リストに追加
        if (!list.Contains(slot))
        {   
            list[slotNum] = slot;
            //  初期化
            slot.Initialize(this, data.itemType);
            slot.DataInitialize(trans, data, slotNum);
        }

        int itemAmont = 0;
        switch (data.itemType)
        {
            case BerItemType.Weapon:
                if (data is WeaponData weapon)
                {
                    itemAmont = weapon.maxAmmo;
                }

                break;
            case BerItemType.Healing:
                break;
            case BerItemType.Throwing:
                break;
        }

        //  UI更新:アイコン
        slot.UpdateUI(itemAmont);
        //  UI更新:スロット
        SlotALLUpdate();
    }

    /// <summary>
    /// スロット全体の更新
    /// </summary>
    public void SlotALLUpdate()
    {
        foreach (var holder in ItemHolderList)
        {
            holder.SlotUpdate();
        }
    }

    /// <summary>
    /// アイテムの削除
    /// </summary>
    /// <param name="slotNum"></param>
    /// <param name="data"></param>
    /// <param name="prefab"></param>
    /// <param name="trans"></param>
    /// <param name="list"></param>
    public void RemoveItemSlot(
        int slotNum,
        ItemData data,
        List<IS_Item> list
       )
    {
        //  入っているデータを削除
        list[slotNum].DeleteData();

        //  リストの要素を空にする
        list[slotNum] = null;
    }

    public ItemData GetItemData(int slotNumber)
    {
        return im.GetItem(slotNumber);
    }

    #endregion

    #region 弾薬・建材 /イベント発火

    /// <summary>
    /// 弾薬の更新
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <param name="changeType"></param>
    private void UpdateAmmo(AmmoType type, int amount, ResourceChangeType changeType)
    {
        switch (changeType)
        {
            case ResourceChangeType.AddedNew:
                InitializeSlot(AmmoListPrefab, ammoListTrans, ammoList, type, amount);
                break;
            case ResourceChangeType.Updated:
                UpdateSlot(ammoList, type, amount);
                break;
            case ResourceChangeType.Removed:
                RemovedSlot(ammoList, type, amount);
                break;
        }
    }

    /// <summary>
    /// 建材の更新 
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <param name="changeType"></param>
    private void UpdateBIldMat(BildingMatType type, int amount, ResourceChangeType changeType)
    {
        switch (changeType)
        {
            case ResourceChangeType.AddedNew:
                InitializeSlot(BIldMatListPrefab, BildMatListTrans, BIldMatList, type, amount);
                break;
            case ResourceChangeType.Updated:
                UpdateSlot(BIldMatList, type, amount);
                break;
            case ResourceChangeType.Removed:
                RemovedSlot(BIldMatList, type, amount);
                break;
        }
    }

    #endregion

    #region 共通関数

    /// <summary>
    /// 新規スロット生成
    /// </summary>
    /// <typeparam name="TSlot"></typeparam>
    /// <typeparam name="TEnum">AmmoType or BildingMatType</typeparam>
    /// <param name="prefab">生成するプレファブ</param>
    /// <param name="listTrans">プレファブを置く場所</param>
    /// <param name="list">置いておくlist</param>
    /// <param name="type">アイテムの種類(AmmoType,BildingMatType)</param>
    private void InitializeSlot<TSlot, TEnum>(
    GameObject prefab,
    Transform listTrans,
    List<TSlot> list,
    TEnum type,
    int amount)
    where TSlot : InventorySlotBase<TEnum>
    where TEnum : Enum
    {
        TSlot slot = null;

        foreach (var item in list)
        {
            //  ２つのTEnum値が同じなら
            if (EqualityComparer<TEnum>.Default.Equals(type, item.GetResourceType()))
            {
                //  同Typeのアイテムを見つけられたらslotに代入
                slot = item;
                slot.Open();
                break;
            }
        }

        //  slotが見つからなかった時
        if (slot == null)
        {
            //  生成
            GameObject ob = Instantiate(prefab, listTrans);
            //  特定のジェネリック型を持つInventorySlotBaseを取得
            slot = ob.GetComponent<TSlot>();
        }

        //  リストに追加
        if (!list.Contains(slot))
        {
            list.Add(slot);
            //  初期化
            slot.Initialize(this, type);
        }

        //  UI更新
        slot.UpdateUI(amount);
    }

    /// <summary>
    /// 既存スロットの更新
    /// </summary>
    /// <typeparam name="TSlot"></typeparam>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="list"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    private void UpdateSlot<TSlot, TEnum>(
    List<TSlot> list,
    TEnum type,
    int amount)
    where TSlot : InventorySlotBase<TEnum>
    where TEnum : Enum
    {
        foreach (var item in list)
        {
            //  ２つのTEnum値が同じなら
            if (EqualityComparer<TEnum>.Default.Equals(type, item.GetResourceType()))
            {
                item.UpdateUI(amount);
                break;
            }
        }
    }

    /// <summary>
    /// 既存スロットの削除
    /// </summary>
    /// <typeparam name="TSlot"></typeparam>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="list"></param>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    private void RemovedSlot<TSlot, TEnum>(
    List<TSlot> list,
    TEnum type,
    int amount)
    where TSlot : InventorySlotBase<TEnum>
    where TEnum : Enum
    {
        TSlot targetItem = null;

        foreach (var item in list)
        {
            //  指定したtypeと操作しようとしているクラスのTypeが同じなら(２つのTEnum値が同じなら)
            if (EqualityComparer<TEnum>.Default.Equals(type, item.GetResourceType()))
            {
                //  アイコン内の数値が0以下なら
                if (item.Getquantity() <= 0)
                {
                    //  そのSlot(InventorySlotBase<TEnum>を継承したクラス)を格納
                    targetItem = item;
                }
            }
        }

        //  インベントリ内のリソース削除
        im?.RemoveResouce(type);
    }


    #endregion

    #region 表示切替

    public override void Show()
    {
        rect.DOAnchorPos(showPos, toggleTime);
    }

    public override void Hide()
    {
        rect.DOAnchorPos(hidePos, toggleTime);
    }

    #endregion

    #region ドロップ


    /// <summary>
    /// アイテム系の大本のドロップ処理
    /// </summary>
    /// <param name="eventData"></param>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log(" OnDrop が呼ばれました！"); // これが出るか確認

        GameObject dropObject = eventData.pointerDrag;
        Debug.Log($"ドロップオブジェクト: {dropObject?.name}");

        if (dropObject != null)
        {
            IInventorySlot isb = dropObject.GetComponent<IInventorySlot>();
            if (isb != null)
            {
                Transform target = null;
                switch (isb.GetItemType())
                {
                    case ItemType.Item:
                        Transform targetTrans = ItemListTrans;

                        if (isb is IS_Item item)
                            targetTrans = ItemHoldersTrams[item.slotNumber];

                        target = targetTrans;

                        break;
                    case ItemType.Ammo:
                        target = ammoListTrans;
                        break;
                    case ItemType.BildingMat:
                        target = BildMatListTrans;
                        break;
                    default:
                        break;
                }

                dropObject.transform.SetParent(target);
            }
        }
    }

    #endregion
}

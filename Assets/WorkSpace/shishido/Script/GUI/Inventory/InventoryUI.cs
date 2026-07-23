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

public class InventoryUI : UIBase, IDropHandler
{


    public InventoryManager im { private set; get; }

    //  表示切り替え変数
    private Vector2 showPos = new Vector2(-10, -30);
    private Vector2 hidePos = new Vector2(400, -30);
    private float toggleTime = 0.2f;

    //  アイコンの配置場所
    [SerializeField] private Transform BildMatListTrans;
    [SerializeField] private Transform ammoListTrans;
    [SerializeField] private Transform ItemListTrans;

    //  プレファブ
    [SerializeField, Header("弾アイコンPrefab")]
    private GameObject AmmoListPrefab;
    [SerializeField, Header("建材アイコンPrefab")]
    private GameObject BIldMatListPrefab;


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

        //  イベント登録
        im.OnChangeAmmo += UpdateAmmo;
        im.OnChangeBildMat += UpdateBIldMat;
    }

    void Update()
    {

    }

    #region イベント発火関数

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
                        target = ItemListTrans;
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

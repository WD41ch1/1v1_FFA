using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class InventoryUI : UIBase
{
    //  表示切り替え変数
    private Vector2 showPos = new Vector2(-10, -30);
    private Vector2 hidePos = new Vector2(400, -30);
    private float toggleTime = 0.2f;

    private InventoryManager im;

    [SerializeField]
    private Transform BildMatListTrans;
    [SerializeField]
    private Transform ammoListTrans;
    [SerializeField]
    private Transform ItemListTrans;

    [SerializeField, Header("弾アイコンPrefab")]
    private GameObject AmmoListPrefab;
    private List<IS_Ammo> ammoList = new List<IS_Ammo>();

    [SerializeField, Header("建材アイコンPrefab")]
    private GameObject BIldMatListPrefab;
    private List<IS_BIldMat> BIldMatList = new List<IS_BIldMat>();

    protected override void OnInitialize()
    {
        im = myPlayer.inventoryManager;
        rect = GetComponent<RectTransform>();

        //  イベント登録
        im.OnAddAmmo += UpdateAmmo;
        im.OnAddBildMat += UpdateBIldMat;
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
                CreateSlot(AmmoListPrefab, ammoListTrans, ammoList, type, amount);
                break;
            case ResourceChangeType.Updated:
                UpdateSlot(ammoList, type, amount);
                break;
            case ResourceChangeType.Removed:
                break;
        }
    }
    private void UpdateBIldMat(BildingMatType type, int amount, ResourceChangeType changeType)
    {
        switch (changeType)
        {
            case ResourceChangeType.AddedNew:
                CreateSlot(BIldMatListPrefab, BildMatListTrans, BIldMatList, type, amount);
                break;
            case ResourceChangeType.Updated:
                UpdateSlot(BIldMatList, type, amount);
                break;
            case ResourceChangeType.Removed:
                break;
        }
    }

    #endregion

    #region 共通関数

    /// <summary>
    /// 新規スロット生成
    /// </summary>
    /// <typeparam name="TSlot"></typeparam>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="prefab">生成するプレファブ</param>
    /// <param name="listTrans">プレファブを置く場所</param>
    /// <param name="list">置いておくlist</param>
    /// <param name="type">アイテムの種類(AmmoType,BildingMatType)</param>
    private void CreateSlot<TSlot, TEnum>(
    GameObject prefab,
    Transform listTrans,
    List<TSlot> list,
    TEnum type,
    float amount)
    where TSlot : InventorySlotBase<TEnum>
    where TEnum : Enum
    {
        //  生成
        GameObject ob = Instantiate(prefab, listTrans);
        //  特定のジェネリック型を持つInventorySlotBaseを取得
        TSlot slot = ob.GetComponent<TSlot>();
        //  初期化
        slot.Initialize(gui, type);
        //  UI更新
        slot.UpdateUI(amount);
        //  リストに追加
        list.Add(slot);
    }

    private void UpdateSlot<TSlot, TEnum>(
    List<TSlot> list,
    TEnum type,
    float amount)
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

}

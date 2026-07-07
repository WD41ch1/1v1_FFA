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
public class EquipmentManager : MonoBehaviour
{
    [SerializeField, Header("アイテム生成場所")]
    private Transform itemSocket;
    //  現在装備スロット番号
    private int currentSlot;
    //  現在装備しているもの
    private ItemBase currentItem;
    //  生成したアイテム
    private GameObject createItem;

    //  装備通知処理
    public event Action<int> OnEquipChanged;

    /// <summary>
    /// アイテムの装備
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="slotNum"></param>
    public void Equip(PlayerManager owner, int slotNum)
    {
        ItemData data;
        //  装備したいアイテム情報を取得
        if (slotNum == PICKEL_SLOT)
            data = owner.inventoryManager.pickel;
        else
            data = owner.inventoryManager.GetItem(slotNum);

        if (data == null) return;

        currentSlot = slotNum;
        OnEquipChanged.Invoke(currentSlot);
        CreateEquipItem(owner, data);
    }

    /// <summary>
    /// 装備アイテムの生成
    /// </summary>
    private void CreateEquipItem(PlayerManager owner, ItemData data)
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
        currentItem.Initialize(owner, data);
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
    public ItemBase GetcurrentItem()
    {
        return currentItem;
    }

    /// <summary>
    /// 次のスロットアイテムを装備
    /// </summary>
    /// <param name="owner"></param>
    public void NextItemEquip(PlayerManager owner)
    {
        int index = currentSlot;

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
        int index = currentSlot;

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

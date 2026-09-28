using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static GameConst;

public class HotbarUI : UIBase
{
    public BerSlot PickaxaSlot;
    public List<BerSlot> SlotIcons;

    private void Awake()
    {
        SlotIcons = new List<BerSlot>(GetComponentsInChildren<BerSlot>());
        //  Pickelだけ除外 + 別で番号割り当て
        SlotIcons.Remove(PickaxaSlot);
        PickaxaSlot.SetNumber(PICKEL_SLOT);
        PickaxaSlot.OnSelected(true);

        for (int i = 0; i < SlotIcons.Count; i++)
        {
            SlotIcons[i].SetNumber(i);
            SlotIcons[i].OnSelected(false);
        }
    }
    protected override void OnInitialize()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        //  イベント登録
        myPlayer.equipmentManager.OnEquipChanged += UpdateSelectFrame;
        myPlayer.inventoryManager.OnAddHotbarItem += UpdateSlotIcon;
        myPlayer.inventoryManager.OnSwapHotbarItem += SwapSlotData;
        myPlayer.inventoryManager.OnRemoveItem += UpdateSlotIcon;
        myPlayer.inventoryManager.OnUpdateItem += UpdateSlotCounter;
    }

    /// <summary>
    /// 装備中アイコン更新
    /// </summary>
    /// <param name="num"></param>
    public void UpdateSelectFrame(int num)
    {
        foreach (BerSlot slot in SlotIcons)
        {
            if (slot.slotNumber == num)
                slot.OnSelected(true);
            else
                slot.OnSelected(false);
        }
    }

    /// <summary>
    /// アイテム取得時のスロットアイコン更新
    /// </summary>
    /// <param name="num"></param>
    public void UpdateSlotIcon(int num)
    {
        ShowItemIcon(num);
    }

    /// <summary>
    /// スロットアイコンのカウンター更新
    /// </summary>
    /// <param name="num"></param>
    public void UpdateSlotCounter(int num,int count)
    {
        SlotIcons[num].UpdateCounter(count);
    }

    /// <summary>
    ///  アイテムアイコン表示
    /// </summary>
    /// <param name="number"></param>
    public void ShowItemIcon(int number)
    {
        if (!isInitialized ||
            SlotIcons == null || SlotIcons.Count < number + 1 ||
            myPlayer.inventoryManager == null)
            return;

        //  指定番号のアイテムデータを取得
        ItemData itemData = myPlayer.inventoryManager.GetItem(number);

        //  データがあれば対応するスロット番号にデータをセット
        if (itemData != null)
            SlotIcons[number].SetData(itemData);
        else
            SlotIcons[number].RemoveData();
    }

    /// <summary>
    /// スワップが時のデータ更新
    /// </summary>
    /// <param name="indexA"></param>
    /// <param name="indexB"></param>
    public void SwapSlotData(int indexA, int indexB)
    {
        //  変更が入ったデータを更新する
        ShowItemIcon(indexA);
        ShowItemIcon(indexB);
    }
}

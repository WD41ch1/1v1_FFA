using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        myPlayer.inventoryManager.OnAddItem += UpdateSlotIcon;
        myPlayer.inventoryManager.OnRemoveItem += UpdateSlotIcon;
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

    //  アイテムアイコン表示
    public void ShowItemIcon(int number)
    {
        if (!isInitialized ||
            SlotIcons == null || SlotIcons.Count < number + 1 ||
            myPlayer.inventoryManager == null)
            return;

        ItemData itemData = myPlayer.inventoryManager.GetItem(number);
        if (itemData != null)
            SlotIcons[number].SetData(itemData);
        else
            SlotIcons[number].RemoveData();
    }
}

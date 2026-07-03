using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : UIBase
{
    public Image PickaxaSlot;
    public List<Image> SlotIcons = new List<Image>();

    protected override void OnInitialize()
    {

    }

    void Update()
    {
        ShowItemIcon(0);
        ShowItemIcon(1);
        ShowItemIcon(2);
        ShowItemIcon(3);
        ShowItemIcon(4);
    }

    public void ShowItemIcon(int number)
    {
        if (!isInitialized ||
            SlotIcons == null || SlotIcons.Count < number + 1 ||
            myPlayer.inventoryManager == null)
            return;

        ItemData itemData = myPlayer.inventoryManager.GetItem(number);
        if (itemData != null)
            SlotIcons[number].sprite = itemData.itemIcon;
    }
}

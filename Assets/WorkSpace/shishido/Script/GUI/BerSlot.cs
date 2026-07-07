using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BerSlot : MonoBehaviour
{
    public int slotNumber { private set; get; }

    public Image ItemIcon;
    public Image rarityIcon;
    public Image SelectFrame;

    private void Awake()
    {
    }
    public void SetNumber(int num)
    {
        slotNumber = num;
    }

    public void OnSelected(bool flag)
    {
        SelectFrame.gameObject.SetActive(flag);
    }

    public void SetData(ItemData data)
    {
        if (data == null) return;

        ItemIcon.sprite = data.itemIcon;
        rarityIcon = null;
    }

}

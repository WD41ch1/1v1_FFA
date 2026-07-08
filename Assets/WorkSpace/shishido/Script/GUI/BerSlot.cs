using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BerSlot : MonoBehaviour
{
    public int slotNumber { private set; get; }

    public Image ItemIcon;
    public Image rarityIcon;
    public Image SelectedFrame;
    public Sprite emptyFrame;


    private void Awake()
    {
        ItemIcon.sprite = emptyFrame;
    }
    public void SetNumber(int num)
    {
        slotNumber = num;
    }

    public void OnSelected(bool flag)
    {
        SelectedFrame.gameObject.SetActive(flag);
    }

    public void SetData(ItemData data)
    {
        if (data == null) return;

        ItemIcon.sprite = data.itemIcon;
        rarityIcon = null;
    }
    public void RemoveData()
    {
        ItemIcon.sprite = emptyFrame;
        rarityIcon = null;
    }

}

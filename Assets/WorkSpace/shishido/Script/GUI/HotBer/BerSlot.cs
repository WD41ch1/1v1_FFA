using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class BerSlot : MonoBehaviour
{
    public int slotNumber { private set; get; }

    public Image ItemIcon;
    public Image rarityIcon;
    public Image SelectedFrame;
    public Sprite emptyFrame;
    public TextMeshProUGUI counter;

    private bool isInitialized = false;
    private Vector3 initPos;

    private void Awake()
    {
        ItemIcon.sprite = emptyFrame;
    }

    private void Start()
    {
        initPos = transform.position;
        isInitialized = true;
    }
    public void SetNumber(int num)
    {
        slotNumber = num;
    }

    /// <summary>
    /// 選択中
    /// </summary>
    /// <param name="flag"></param>
    public void OnSelected(bool flag)
    {
        SelectedFrame.gameObject.SetActive(flag);

        SlotSelectMove(flag);
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
        counter.text= string.Empty;
    }

    private void SlotSelectMove(bool flag)
    {
        //  初期化が終わってるか
        if (!isInitialized) return;

        if (flag)
            transform.DOMove(new Vector3(initPos.x, initPos.y + 5, initPos.z), 0.5f).SetUpdate(true);
        else
            transform.DOMove(initPos, 0.5f).SetUpdate(true);
    }

    public void UpdateCounter(int count)
    {
        counter.text = count.ToString();
    }
}

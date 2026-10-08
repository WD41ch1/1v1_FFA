using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryUI_ItemDrop : UIBase, IDropHandler
{
    public InventoryManager im { private set; get; }
    public InventoryUI iui { private set; get; }

    public int SlotNumber { private set; get; }

    public IS_Item haveHolder = null;

    protected override void OnInitialize()
    {
        im = myPlayer.inventoryManager;
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 設定
    /// </summary>
    /// <param name="num"></param>
    public void Setting(InventoryUI _iui, int num)
    {
        iui = _iui;
        SlotNumber = num;
    }


    /// <summary>
    /// スロットの更新
    /// </summary>
    public void SlotUpdate()
    {
        //  自身についているアイテム(UI)を取得
        IInventorySlot isb = GetComponentInChildren<IInventorySlot>();
        //  何もなければ
        if (isb == null)
        {
            haveHolder = null;
            return;
        }
        //  アイテムであるか
        if (isb is IS_Item item)
        {
            //  アイテム(UI)を格納
            haveHolder = item;
        }
        else
        {
            haveHolder = null;
        }

    }

    /// <summary>
    /// アイテム専用のドロップされたときの処理
    /// </summary>
    /// <param name="eventData"></param>
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log(" OnDrop が呼ばれました！"); // これが出るか確認

        GameObject dropObject = eventData.pointerDrag;

        if (dropObject != null)
        {
            IInventorySlot isb = dropObject.GetComponent<IInventorySlot>();
            if (isb != null)
            {
                switch (isb.GetItemType())
                {
                    case ItemType.Item:
                        Transform target = null;

                        if (isb is IS_Item item)
                        {
                            Debug.Log($"自身の番号:{SlotNumber}|ドロップしたアイテム番号:{item.slotNumber}");
                            //  InventoryManager:SwapItemを呼び、アイテムの入れ替えを行う
                            if (im.SwapItem(item.slotNumber, SlotNumber))
                            {
                                int changeNum = item.slotNumber;
                                //  親とナンバーを自分に設定
                                item.SetNumber(SlotNumber);
                                target = transform;

                                //  このスロットにIS_Itemが入っているかどうか
                                if (haveHolder!= null)
                                {
                                    //  もともと入っていたIS_Itemを移動
                                    haveHolder.gameObject.transform.SetParent(iui.GetHolderTrans(changeNum));
                                    haveHolder.SetNumber(changeNum);
                                }
                            }
                            else
                                target = iui.GetHolderTrans(item.slotNumber);


                            dropObject.transform.SetParent(target);
                            iui.SlotALLUpdate();
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

}

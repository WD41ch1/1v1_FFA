using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IS_Item : InventorySlotBase<BerItemType>
{
    public int slotNumber { private set; get; }

    public Image rarityIcon;

    public ItemData itemData;

    public override void Initialize(InventoryUI _inventory, BerItemType _type)
    {
        //  ItemTypeの設定
        itemType = ItemType.Item;
        //  誰のInventoryか(owner)
        inventory = _inventory;
        //  必要コンポーネント取得
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = inventory.GetcanvasGroup();
        type = _type;
    }
    public override void UpdateUI(int _quantity)
    {

    }

    public override void Droping()
    {
        //  指定数捨てる
        if (!inventory.im.ItemDroping(slotNumber, itemData)) return;
        //  自身を削除
        Destroy(gameObject);

    }

    //public override void DetectDropZone(PointerEventData eventData)
    //{
    //    // UI Raycast を使って、マウス位置の UI 要素を全て取得
    //    var results = new System.Collections.Generic.List<RaycastResult>();
    //    EventSystem.current.RaycastAll(eventData, results);

    //    IDropHandler foundDropZone = null;

    //    // Raycast の結果を全て確認
    //    foreach (var result in results)
    //    {
    //        if (result.gameObject == gameObject)
    //            continue;

    //        IDropHandler dropHandler =
    //            result.gameObject.GetComponent<IDropHandler>();

    //        if (dropHandler == null)
    //            continue;

    //        // 最優先のDropZoneだった場合
    //        if (dropHandler is InventoryUI_ItemDrop)
    //        {
    //            foundDropZone = dropHandler;
    //            break;
    //        }

    //        // 通常DropZoneは一旦保存
    //        if (foundDropZone == null)
    //        {
    //            foundDropZone = dropHandler;
    //        }
    //    }

    //    // ドロップゾーンが変わった場合
    //    if (foundDropZone != currentDropZone)
    //    {
    //        // 前のドロップゾーンから出た
    //        if (currentDropZone != null)
    //        {
    //            Debug.Log("ドロップゾーンから出ました");
    //            // IPointerExitHandler を呼びたい場合
    //            var pointerExit = currentDropZone as IPointerExitHandler;
    //            pointerExit?.OnPointerExit(eventData);
    //        }

    //        // 新しいドロップゾーンに入った
    //        if (foundDropZone != null)
    //        {
    //            Debug.Log("ドロップゾーンに入りました");
    //            // IPointerEnterHandler を呼びたい場合
    //            var pointerEnter = foundDropZone as IPointerEnterHandler;
    //            pointerEnter?.OnPointerEnter(eventData);
    //        }

    //        currentDropZone = foundDropZone;
    //    }
    //}

    public override void Open()
    {
    }

    public override void Hide()
    {
    }

    public void DataInitialize(Transform trans, ItemData data, int slotNum)
    {
        initParent = trans;
        //  アイコン
        icon.sprite = data.itemIcon;

        SetNumber(slotNum);
    }

    /// <summary>
    /// 番号の設定
    /// </summary>
    /// <param name="num"></param>
    public void SetNumber(int num)
    {
        slotNumber = num;
    }
}

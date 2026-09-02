using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class IS_Item : InventorySlotBase<BerItemType>
{
    public int slotNumber { private set; get; }

    public Image ItemIcon;
    public Image rarityIcon;

    public override void Initialize(InventoryUI _inventory, BerItemType _type)
    {
        //  ItemTypeの設定
        itemType = ItemType.Item;
        //  誰のInventoryか(owner)
        inventory = _inventory;
        //  必要コンポーネント取得
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = inventory.GetcanvasGroup();
        initParent = transform.parent;

        //  アイコン
        ItemIcon.sprite = inventory.GetItemData(1).itemIcon;

        type = _type;
    }
    public override void UpdateUI(int _quantity)
    {
    }
    public override void Open()
    {
    }

    public override void Hide()
    {
    }
}

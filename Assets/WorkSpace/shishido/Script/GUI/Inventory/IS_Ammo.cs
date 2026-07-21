using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IS_Ammo : InventorySlotBase<AmmoType>
{

    public override void Initialize(InventoryUI _inventory, AmmoType _type)
    {
        itemType = ItemType.Ammo;
        rectTrans = GetComponent<RectTransform>();
        inventory = _inventory;
        canvasGroup = inventory.GetcanvasGroup();
        initParent = transform.parent;
        type = _type;
    }

    public override void UpdateUI(int _quantity)
    {
        quantity = _quantity;
        if (quantityText != null)
            quantityText.text = quantity.ToString();
    }

    public override void Droping()
    {
        //  指定数捨てる
        inventory?.im.AmmoDroping(type, quantity);
        //  自身を削除
        Destroy(gameObject);
    }

}

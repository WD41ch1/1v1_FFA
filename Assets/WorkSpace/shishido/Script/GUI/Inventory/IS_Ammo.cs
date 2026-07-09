using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IS_Ammo : InventorySlotBase<AmmoType>
{

    public override void Initialize(GUIManager _gui, AmmoType _type)
    {
        itemType = ItemType.Ammo;
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = _gui.canvasGroup;
        type = _type;
    }

    public override void UpdateUI(float _quantity)
    {
        quantity = _quantity;
        if(quantityText != null)
            quantityText.text = quantity.ToString();
    }

    
}

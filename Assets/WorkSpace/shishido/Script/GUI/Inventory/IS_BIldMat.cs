using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IS_BIldMat : InventorySlotBase<BildingMatType>
{
    public override void Initialize(GUIManager _gui, BildingMatType _type)
    {
        itemType = ItemType.BildingMat;
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = _gui.canvasGroup;
        type = _type;
    }

    public override void UpdateUI(float _quantity)
    {
        quantity = _quantity;
        if (quantityText != null)
            quantityText.text = quantity.ToString();
    }

}

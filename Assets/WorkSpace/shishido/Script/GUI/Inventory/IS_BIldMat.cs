using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class IS_BIldMat : InventorySlotBase<BildingMatType>
{
    public override void Initialize(InventoryUI _inventory, BildingMatType _type)
    {
        //  ItemTypeの設定
        itemType = ItemType.BildingMat;
        //  誰のInventoryか(owner)
        inventory = _inventory;
        //  必要コンポーネント取得
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = inventory.GetcanvasGroup();
        initParent = transform.parent;
        if (quantityText == null) quantityText = GetComponentInChildren<TextMeshProUGUI>();
        //  建材タイプ
        type = _type;
    }

    public override void UpdateUI(int _quantity)
    {
        quantity = _quantity;
        if (quantityText != null)
            quantityText.text = quantity.ToString();
        else
            Debug.LogWarning("!");
    }

    public override void Droping()
    {
        //  指定数捨てる
        inventory?.im.MatDroping(type, quantity);

        //  自身を削除
        Hide();
        //Destroy(gameObject);
    }

    public override void Open()
    {
        transform.gameObject.SetActive(true);
    }

    public override void Hide()
    {
        transform.gameObject.SetActive(false);
    }
}

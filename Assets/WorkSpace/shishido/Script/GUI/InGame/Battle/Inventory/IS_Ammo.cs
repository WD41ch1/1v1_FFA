using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class IS_Ammo : InventorySlotBase<AmmoType>
{
    [Header("Icon用スプライト")]
    [SerializeField] private Sprite SmallAmmoIcon;
    [SerializeField] private Sprite MiddleAmmoIcon;
    [SerializeField] private Sprite BigAmmoIcon;
    [SerializeField] private Sprite ShotgunAmmoIcon;

    public override void Initialize(InventoryUI _inventory, AmmoType _type)
    {
        //  ItemTypeの設定
        itemType = ItemType.Ammo;
        //  誰のInventoryか(owner)
        inventory = _inventory;
        //  必要コンポーネント取得
        rectTrans = GetComponent<RectTransform>();
        canvasGroup = inventory.GetcanvasGroup();
        initParent = transform.parent;
        if (quantityText == null) quantityText = GetComponentInChildren<TextMeshProUGUI>();
        //  建材タイプ
        type = _type;
        //  スプライトのセット
        SetSprite();
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

    public override void SetSprite()
    {
        if (icon == null) return;

        switch (type)
        {
            case AmmoType.SmallAmmo:
                itemIcon = SmallAmmoIcon;
                break;
            case AmmoType.MiddleAmmo:
                itemIcon = MiddleAmmoIcon;
                break;
            case AmmoType.BigAmmo:
                itemIcon = BigAmmoIcon;
                break;
            case AmmoType.ShotgunAmmo:
                itemIcon = ShotgunAmmoIcon;
                break;
        }

        icon.sprite = itemIcon;
    }

}

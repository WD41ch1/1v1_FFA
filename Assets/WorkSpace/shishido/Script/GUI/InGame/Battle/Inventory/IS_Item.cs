using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class IS_Item : InventorySlotBase<BerItemType>
{
    public int slotNumber { private set; get; }

    public Image rarityIcon;

    public ItemData itemData;

    public Image timerRing;
    private Coroutine nowCoroutine;

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

    public override void DeleteData()
    {
        //  中身を削除(念のため)
        itemData = null;

        //  自身を削除
        Destroy(gameObject);
    }

    public override void Droping()
    {
        //  指定数捨てる
        if (!inventory.im.ItemDroping(slotNumber, itemData)) return;
        //  自身を削除
        Destroy(gameObject);

    }

    public override void Open()
    {
    }

    public override void Hide()
    {
    }

    public void DataInitialize(Transform trans, ItemData data, int slotNum)
    {
        initParent = trans;

        itemData = data;

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

    public void StertTimer(float duration)
    {
        if (timerRing == null) return;

        timerRing.gameObject.SetActive(true);

        nowCoroutine = StartCoroutine(ReloadTimer(duration));
    }

    public void CancelTimer()
    {
        if (nowCoroutine != null)
        {
            StopCoroutine(nowCoroutine);
            nowCoroutine = null;
            timerRing.fillAmount = 0f;
            timerRing.gameObject.SetActive(false);
        }
    }

    private IEnumerator ReloadTimer(float duration)
    {
        float startValue = timerRing.fillAmount;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            timerRing.fillAmount = Mathf.Lerp(startValue, 0f, elapsed / duration);

            yield return null;
        }

        timerRing.fillAmount = 0f;

        timerRing.gameObject.SetActive(false);
        nowCoroutine = null;
    }
}

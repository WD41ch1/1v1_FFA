using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 現在何を装備しているか
 武器の生成
 武器の破棄
 使用処理の呼び出し
 を担当
 */
public class EquipmentManager : MonoBehaviour
{
    [SerializeField, Header("アイテム生成場所")]
    private Transform itemSocket;

    //  現在装備しているもの
    private ItemBase currentItem;
    //  生成したアイテム
    private GameObject createItem;

    public ItemBase GetcurrentItem()
    {
        return currentItem;
    }

    /// <summary>
    /// アイテムの装備
    /// 武器の生成など
    /// </summary>
    public void Equip(TC_Character owner, ItemData ItemData)
    {
        if (itemSocket == null || ItemData == null)
            return;

        //  アイテムの生成
        createItem = Instantiate(ItemData.itemPrefab, itemSocket);
        //  生成したアイテムのItemBaseを取得
        currentItem = createItem.GetComponent<ItemBase>();
        //  装備アイテムの初期化
        currentItem.Initialize(owner,ItemData);
    }

    /// <summary>
    /// アイテムの破棄
    /// </summary>
    public void UnEquip()
    {
        if(createItem == null) return;

        Destroy(createItem);
        createItem = null;
        currentItem = null;
    }
}

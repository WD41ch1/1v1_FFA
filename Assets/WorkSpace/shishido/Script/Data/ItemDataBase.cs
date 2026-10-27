using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemDatabase",
    menuName = "Game/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Header("全アイテム一覧")]
    [SerializeField]
    private List<ItemData> items = new();

    // IDからアイテムを検索するための辞書
    private Dictionary<int, ItemData> itemDictionary;

    /// <summary>
    /// アイテム一覧から検索用辞書を作成する
    /// </summary>
    public void Initialize()
    {
        itemDictionary = new Dictionary<int, ItemData>();

        foreach (ItemData item in items)
        {
            if (item == null)
            {
                Debug.LogWarning("ItemDatabaseに空の要素があります。");
                continue;
            }

            // itemIdは既存のItemDataの変数名に合わせる
            if (!itemDictionary.TryAdd(item.itemId, item))
            {
                Debug.LogError(
                    $"アイテムIDが重複しています: {item.itemId}");
            }
        }
    }

    /// <summary>
    /// IDからアイテムデータを取得する
    /// </summary>
    public bool TryGetItem(int itemId, out ItemData item)
    {
        item = null;

        if (itemDictionary == null)
        {
            Debug.LogError(
                "ItemDatabaseが初期化されていません。");
            return false;
        }

        return itemDictionary.TryGetValue(itemId, out item);
    }
}
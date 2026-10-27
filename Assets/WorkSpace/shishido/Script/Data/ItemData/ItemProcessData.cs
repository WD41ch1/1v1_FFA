using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item Process Data")]
public class ItemProcessData : ScriptableObject
{
    public int itemId;

    // このアイテムの使用処理を持つPrefab
    public ItemBase processPrefab;
}
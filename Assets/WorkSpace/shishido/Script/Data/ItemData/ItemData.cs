using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponAnimationType
{
    None = 0,
    Pistol = 1,
    AssaultRifle = 2,
    Pickaxe = 3
}


[CreateAssetMenu]
public class ItemData : ScriptableObject
{
    public int itemId;

    [Header("--inventory表示アイコン--")]
    [Header("inventory表示アイコン")]
    public Sprite itemIcon;

    [Header("レアリティバックグラウンドカラー")]
    public Sprite rarityBackGroundIcon;

    [Header("--基本情報--")]
    [Header("アイテム名")]
    public string ItemName;

    [Header("アイテムのタイプ")]
    public BerItemType itemType;

    [Header("Prefab")]
    public GameObject AppearancePrefab;
    public GameObject wearPrefab;

    [Header("スタック")]
    [Min(1)]
    public int maxStack = 1;

    /// <summary>
    /// このアイテムが1枠に所持できる最大数。
    /// 派生クラスで上書き可能。
    /// </summary>
    public virtual int MaxStack =>
        Mathf.Max(1, maxStack);

}

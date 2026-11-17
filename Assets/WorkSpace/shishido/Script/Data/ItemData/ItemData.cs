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

    [Header("アイテム名")]
    public string ItemName;

    [Header("アイテムのタイプ")]
    public BerItemType itemType;

    [Header("装備時のアニメーション種類")]
    public WeaponAnimationType weaponAnimationType =
        WeaponAnimationType.None;

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
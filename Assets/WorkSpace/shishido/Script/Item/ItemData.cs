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

    [Header("アイテムの見た目")]
    public GameObject itemPrefab;

    [Header("1スロットに重ねられる個数")]
    public int maxStack;

}

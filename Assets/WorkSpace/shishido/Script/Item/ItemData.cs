using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ItemData : ScriptableObject
{
    [Header("--inventory表示アイコン--")]
    [Header("inventory表示アイコン")]
    public Sprite itemIcon;

    [Header("レアリティバックグラウンドカラー")]
    public Sprite rarityBackGroundIcon;

    [Header("--基本情報--")]
    [Header("武器名")]
    public string ItemName;

    [Header("アイテムの見た目")]
    public GameObject itemPrefab;

    [Header("1スロットに重ねられる個数")]
    public int maxStack;

}

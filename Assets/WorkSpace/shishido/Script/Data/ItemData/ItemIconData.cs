using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item Icon Data")]

public class ItemIconData : ScriptableObject
{
    public int itemId;

    [Header("IconSprite")]
    [Tooltip("inventory表示アイコンの設定")]
    public Sprite icon;
}

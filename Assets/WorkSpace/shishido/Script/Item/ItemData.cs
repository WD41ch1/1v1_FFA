using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ItemData : ScriptableObject
{
    [Header("inventory表示アイコン")]
    public Sprite icon;

    [Header("武器名")]
    public string ItemName;
}

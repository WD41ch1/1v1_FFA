using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PickaxeData : ItemData
{
    //==============================================
    //      基本情報
    //==============================================
    [Header("ダメージ")]
    public float weaponDamage;
    [Header("連射速度")]
    public float shakeSpeed;
}

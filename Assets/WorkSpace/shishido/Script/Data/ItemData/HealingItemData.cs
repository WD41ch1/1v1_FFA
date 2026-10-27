using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class HealingItemData : ItemData
{
    //==============================================
    //      基本情報
    //==============================================
    [Header("回復量")]
    public float healingValue;
    [Header("回復上限値")]
    public float maxHealingAmount;
    [Header("回復時間")]
    public float healingTiem;
    [Header("使用してから再使用できるまでのクールタイム")]
    public float healingCoolTiem;

}

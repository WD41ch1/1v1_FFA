using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotHealth : MonoBehaviour
{
    [SerializeField,Header("最大HP")]
    public float maxHealth = 100;
    [SerializeField,Header("最大シールド")]
    public float maxShielde = 100;
    [SerializeField,Header("HP")]
    public float Health = 100;
    [SerializeField,Header("シールド")]
    public float Shielde = 100;
}

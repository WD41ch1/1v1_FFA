using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item Model Data")]
public class AppearanceModelData : ScriptableObject
{
    public int itemId;

    [Header("Prefab")]
    public GameObject AppearancePrefab;

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Item/Item Model Data")]
public class AppearanceModelData : ScriptableObject
{
    public int itemId;

    [Header("Prefab")]
    [Header("Local上")]
    public GameObject AppearancePrefab;
    [Header("Net上")]
    public GameObject AppearanceNetPrefab;
}

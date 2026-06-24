using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSceneManager : MonoBehaviour
{
    /// <summary>
    /// 実際は拾うアイテムに持っている
    /// </summary>
    [SerializeField]
    public ItemData ItemData;

    private InventoryManager im;
    private EquipmentManager em;

    void Start()
    {
        im = GetComponent<InventoryManager>();
        em = GetComponent<EquipmentManager>();
    }


}

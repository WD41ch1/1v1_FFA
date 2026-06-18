using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestSceneManager : MonoBehaviour
{
    [SerializeField]
    public ItemData ItemData;

    private InventoryManager im;

    void Start()
    {
        im = GetComponent<InventoryManager>();
    }

    // Update is called once per frame
    void Update()
    {
        TestInventory();
        TestPlayer();
    }

    public void TestInventory()
    {
        //  アイテムを取得
        if (Input.GetKeyDown(KeyCode.F))
        {
            im.AddItem(ItemData);
        }
        //  アイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            im.AddItem(ItemData);
        }
    }

    public void TestPlayer()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //im.GetItem(0).UsePrimary();
        }

        if (Input.GetMouseButtonDown(1))
        {
            //im.GetItem(0).UseSecondary();
        }
    }

}

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
            em.Equip(this.gameObject,im.GetItem(0));
        }
    }

    public void TestPlayer()
    {
        //  左クリック処理
        if (Input.GetMouseButtonDown(0))
        {
            em.GetcurrentItem()?.UsePrimary();
        }

        //  右クリック処理
        if (Input.GetMouseButtonDown(1))
        {
            em.GetcurrentItem()?.UseSecondary();
        }

        //  リロード
        if (Input.GetKeyDown(KeyCode.R))
        {
            em.GetcurrentItem()?.UseReload();
        }
        //  リロード
        if (Input.GetKeyDown(KeyCode.E))
        {
            em.UnEquip();
        }
    }

}

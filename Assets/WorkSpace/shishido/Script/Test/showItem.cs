using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class showItem : MonoBehaviour
{
    [SerializeField]
    public PlayerManager pm;

    public TextMeshProUGUI t1;
    public TextMeshProUGUI t2;
    public TextMeshProUGUI t3;
    public TextMeshProUGUI t4;
    public TextMeshProUGUI t5;
    public TextMeshProUGUI t6;
    public TextMeshProUGUI t20;


    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        InventoryManager im = pm.inventoryManager;

        t1.text = im.GetAmmo(AmmoType.SmallAmmo).ToString();
        t2.text = im.GetAmmo(AmmoType.MiddleAmmo).ToString();
        t3.text = im.GetAmmo(AmmoType.BigAmmo).ToString();
        t4.text = im.GetMat(BildingMatType.Wood).ToString();
        t5.text = im.GetMat(BildingMatType.Brick).ToString();
        t6.text = im.GetMat(BildingMatType.Iron).ToString();
        showAmmoRemaining();
    }

    public void showAmmoRemaining()
    {
        //  現在装備しているアイテムを取得
        ItemBase ib = pm.equipmentManager.GetcurrentItem();

        //  取得したアイテムがNULLでなく、かつ継承先がWeaponBaseなら
        if (ib != null && ib is WeaponBase)
        {
            WeaponBase weapon = (WeaponBase)ib;
            t20.text = weapon.GetcurrentAmmo().ToString();
        }
    }

}

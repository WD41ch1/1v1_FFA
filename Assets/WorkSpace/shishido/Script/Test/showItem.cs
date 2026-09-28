using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class showItem : MonoBehaviour
{
    [SerializeField]
    public PlayerManager pm;

    public InventoryManager im;

    public TextMeshProUGUI t1;
    public TextMeshProUGUI t2;
    public TextMeshProUGUI t3;
    public TextMeshProUGUI t4;
    public TextMeshProUGUI t11;
    public TextMeshProUGUI t12;
    public TextMeshProUGUI t13;
    public TextMeshProUGUI t20;

    private bool isInit = false;

    void Start()
    {
        StartCoroutine(GetPlayer());
    }

    public IEnumerator GetPlayer()
    {
        yield return null;

        pm = GUIManager.instance.myPlayer;
        im = pm.inventoryManager;

        isInit = true;
    }

    // Update is called once per frame
    void Update()
    {

        if (!isInit) return;

        t1.text = im?.GetAmmo(AmmoType.SmallAmmo).ToString();
        t2.text = im?.GetAmmo(AmmoType.MiddleAmmo).ToString();
        t3.text = im?.GetAmmo(AmmoType.BigAmmo).ToString();
        t4.text = im?.GetAmmo(AmmoType.ShotgunAmmo).ToString();
        t11.text = im?.GetMat(BildingMatType.Wood).ToString();
        t12.text = im?.GetMat(BildingMatType.Brick).ToString();
        t13.text = im?.GetMat(BildingMatType.Iron).ToString();
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
        else
        {
            t20.text = "No equipment".ToString();
        }


    }

}

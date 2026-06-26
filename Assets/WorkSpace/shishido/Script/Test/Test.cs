using System.Collections;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    public PlayerManager pm;

    public EquipmentManager em;
    public InventoryManager im;
    //  test用UI表示
    public showItem si;

    [Header("アイテムデータ(実際は拾うアイテムについているもの)")]
    public ItemData itemData;
    public ItemData itemData2;


    void Start()
    {
        Debug.Log(Application.dataPath);
        if (pm == null)
            pm = GetComponent<PlayerManager>();


        StartCoroutine(GetCoroutine());
    }
    void Update()
    {
        //  デバッグ用
        TestInventory();
        TestPlayer();
    }

    #region デバッグ用関数

    private IEnumerator GetCoroutine()
    {
        yield return null;
        im = pm.inventoryManager;
        em = pm.equipmentManager;

    }

    public void showAmmoRemaining(int value)
    {
        //if (si != null)
        //    si.showAmmoRemaining(value);
    }

    public void TestInventoryInit()
    {
        im.AddAmmo(AmmoType.SmallAmmo, 200);
        im.AddAmmo(AmmoType.MiddleAmmo, 400);
        im.AddAmmo(AmmoType.BigAmmo, 50);
        im.AddAmmo(AmmoType.ShotgunAmmo, 100);
        im.AddBildMat(BildingMatType.Wood, 500);
        im.AddBildMat(BildingMatType.Brick, 500);
        im.AddBildMat(BildingMatType.Iron, 500);
    }
    public void TestInventory()
    {
        //  アイテムを取得
        if (Input.GetKeyDown(KeyCode.F))
        {
            im.AddItem(itemData);
            im.AddItem(itemData2);
            TestInventoryInit();

        }
        //  スロット1のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            em.Equip(this.pm, im.GetItem(0));
        }
        //  スロット2のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            em.Equip(this.pm, im.GetItem(1));
        }
        //  スロット3のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            em.Equip(this.pm, im.GetItem(2));
        }
        //  スロット4のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            em.Equip(this.pm, im.GetItem(3));
        }
        //  スロット5のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            em.Equip(this.pm, im.GetItem(4));
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
    #endregion

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<IPickupable>(out var pickup))
        {
            pickup?.Pickup(pm);
        }
    }
}
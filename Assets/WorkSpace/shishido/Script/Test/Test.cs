using FishNet.Demo.AdditiveScenes;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;
using static UnityEditor.Timeline.TimelinePlaybackControls;

public class Test : MonoBehaviour
{
    [SerializeField]
    public PlayerManager pm;

    public EquipmentManager em;
    public InventoryManager im;

    public GUIManager gui;

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

        em = pm.equipmentManager;
        im = pm.inventoryManager;
        //gui = pm.guiManager;
    }
    void Update()
    {
        //  デバッグ用
        TestInventory();
        TestPlayer();
    }

    #region デバッグ用関数

    public void showAmmoRemaining(int value)
    {
        //if (si != null)
        //    si.showAmmoRemaining(value);
    }

    public void TestInventoryInit()
    {
        im.AddAmmo(AmmoType.SmallAmmo, 200);
        im.AddAmmo(AmmoType.MiddleAmmo, 40);
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
        //  ピッケルを装備
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            em.Equip(this.pm, PICKEL_SLOT);
        }
        //  スロット1のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            em.Equip(this.pm, ITEM_SLOT_1);
        }
        //  スロット2のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            em.Equip(this.pm, ITEM_SLOT_2);
        }
        //  スロット3のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            em.Equip(this.pm, ITEM_SLOT_3);
        }
        //  スロット4のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            em.Equip(this.pm, ITEM_SLOT_4);
        }
        //  スロット5のアイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            em.Equip(this.pm, ITEM_SLOT_5);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0)
        {
            //Debug.Log("上にスクロール");
            em.PreviousItemEquip(this.pm);
        }
        else if (scroll < 0)
        {
            //Debug.Log("下にスクロール");
            em.NextItemEquip(this.pm);
        }


        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            im.RemoveItem(ITEM_SLOT_1);
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            im.RemoveItem(ITEM_SLOT_2);
        }
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            im.RemoveItem(ITEM_SLOT_3);

        }
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            im.RemoveItem(ITEM_SLOT_4);
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            im.RemoveItem(ITEM_SLOT_5);
        }
    }

    private InputAction.CallbackContext cantext;

    public void TestPlayer()
    {

        //  左クリック処理
        if (Input.GetMouseButtonDown(0))
        {
            //em.GetcurrentItem()?.UsePrimary(cantext);
        }
        if (Input.GetMouseButtonUp(0))
        {
            //em.GetcurrentItem()?.UsePrimary(cantext);
        }

        //  右クリック処理
        if (Input.GetMouseButtonDown(1))
        {
            em.GetcurrentItem()?.UseSecondary(true);
        }
        if (Input.GetMouseButtonUp(1))
        {
            em.GetcurrentItem()?.UseSecondary(false);
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

        //  被弾処理
        if (Input.GetKeyDown(KeyCode.K))
        {
            DamageInfo testinfo = new DamageInfo();
            testinfo.Damage = 30;
            testinfo.Attacker = null;
            testinfo.Weapon = null;
            testinfo.IsHeadshot = false;

            pm.playerHealth.TakeDamage(testinfo);
        }
    }
    #endregion

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<IPickupable>(out var pickup))
        {
            pickup?.Pickup(pm);
        }
    }
}
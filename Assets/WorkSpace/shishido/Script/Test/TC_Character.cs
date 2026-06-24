using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XInput;

/// <summary>
/// Playerについているclassを管理するクラス(テスト用)
/// </summary>
public class TC_Character : MonoBehaviour
{
    public PlayerInputController inputController;
    public CharacterController controller;
    public CameraController cameraController;
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;

    [Header("アイテムデータ(実際は拾うアイテムについているもの)")]
    public ItemData itemData;

    void Start()
    {
        GetPlayerClass();
    }

    private void GetPlayerClass()
    {
        inputController = GetComponent<PlayerInputController>();
        controller = GetComponent<CharacterController>();
        cameraController = GetComponent<CameraController>();
        equipmentManager = GetComponent<EquipmentManager>();
        inventoryManager = GetComponent<InventoryManager>();
    }

    void Update()
    {
        //  デバッグ用
        TestInventory();
        TestPlayer();
    }

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeAmmo(
    AmmoType type,
    int amount)
    {
        return inventoryManager.TryConsumeAmmo(
            type,
            amount);
    }

    #region デバッグ用処理関数
    public void TestInventory()
    {
        //  アイテムを取得
        if (Input.GetKeyDown(KeyCode.F))
        {
            inventoryManager.AddItem(itemData);
        }
        //  アイテムを装備
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            equipmentManager.Equip(this, inventoryManager.GetItem(0));
        }
    }

    public void TestPlayer()
    {
        //  左クリック処理
        if (Input.GetMouseButtonDown(0))
        {
            equipmentManager.GetcurrentItem()?.UsePrimary();
        }

        //  右クリック処理
        if (Input.GetMouseButtonDown(1))
        {
            equipmentManager.GetcurrentItem()?.UseSecondary();
        }

        //  リロード
        if (Input.GetKeyDown(KeyCode.R))
        {
            equipmentManager.GetcurrentItem()?.UseReload();
        }
        //  リロード
        if (Input.GetKeyDown(KeyCode.E))
        {
            equipmentManager.UnEquip();
        }
    }
    #endregion


}

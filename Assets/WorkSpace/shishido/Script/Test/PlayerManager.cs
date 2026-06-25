using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XInput;

/// <summary>
/// Playerについているclassを管理するクラス
/// </summary>
public class PlayerManager: MonoBehaviour
{
    public PlayerInputController inputController;
    public CharacterController controller;
    public CameraController cameraController;
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;

    //  デバッグ用UI表示
    public showItem si;

    [Header("アイテムデータ(実際は拾うアイテムについているもの)")]
    public ItemData itemData;
    public ItemData itemData2;

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
   
}

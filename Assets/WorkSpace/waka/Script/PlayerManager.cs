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

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeAmmo(
    AmmoType type,
    int amount,
    out int approvalValue)
    {
        return inventoryManager.TryConsumeAmmo(
            type,
            amount,
            out approvalValue);
    }

}

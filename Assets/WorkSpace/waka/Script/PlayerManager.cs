using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XInput;
using static GameConst;

/// <summary>
/// Playerについているclassを管理するクラス
/// </summary>
public class PlayerManager : MonoBehaviour
{
    [Header("Player種別")]
    [SerializeField]
    private PlayerType playerType;

    /// <summary>
    /// Player種別を外部から取得
    /// </summary>
    public PlayerType PlayerType => playerType;


    [Header("ローカルPlayerかの判別 ※現状は手動")]
    [SerializeField]
    private bool isLocalPlayer;

    public bool IsLocalPlayer => isLocalPlayer;

    #region Components

    [Header("Player Components")]
    public PlayerInputController inputController;
    public CharacterController controller;
    public CameraController cameraController;
    public PlayerHealth playerHealth;
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;

    [Header("UI")]      // UI全体の見た目、処理を管理
    public IGuiInitialize guiManager;

    #endregion

    #region Initialize

    /// <summary>
    /// 必要なコンポーネント取得
    /// </summary>
    private void GetPlayerClass()
    {
        inputController = GetComponent<PlayerInputController>();
        controller = GetComponent<CharacterController>();
        cameraController = Camera.main.gameObject.GetComponent<CameraController>();
        playerHealth = GetComponent<PlayerHealth>();
        equipmentManager = GetComponent<EquipmentManager>();
        inventoryManager = GetComponent<InventoryManager>();


        // HACK:    オンライン対応時 変更予定
        // UIを登録
        switch (playerType)
        {
            case PlayerType.Player:
                if (IsLocalPlayer)
                {
                    guiManager = GUIManager.instance;
                    //if(guiManager is GUIManager manager)
                    //    manager.RegisterPlayer(this);
                }
                break;
            case PlayerType.Bot:
                guiManager = GetComponentInChildren<BOTHealthBer>();
                break;
        }
    }

    /// <summary>
    /// Playerの初期化
    /// </summary>
    private void Initialize()
    {
        // Player・Bot初期化
        switch (playerType)
        {
            case PlayerType.Player:
                //  statusの初期化
                playerHealth?.Initialize(
                    PLAYER_MAXHEALTH,
                    PLAYER_MAXSHIELDE,
                    100,
                    100
                    );

                //  カメラに自身の登録
                cameraController.RegisterPlayer(this);

                break;
            case PlayerType.Bot:
                playerHealth?.Initialize(
                    100,
                    PLAYER_MAXSHIELDE,
                    1,
                    1
                    );

                break;
        }

        // UIを初期化
        guiManager?.RegisterPlayer(this);
        guiManager?.Initialize();

        //  status管理側からのUI更新要求
        playerHealth?.UIUpdateRequest();
    }

    #endregion

    void Start()
    {
        GetPlayerClass();
        Initialize();
    }

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type">建材の種類</param>
    /// <param name="amount">消費要求数</param>
    /// <param name="approvalValue">消費承認数</param>
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

    /// <summary>
    /// 建材消費要求
    /// </summary>
    /// <param name="type">建材の種類</param>
    /// <param name="amount">消費要求数</param>
    /// <param name="approvalValue">消費承認数 ※場合によっては使用しなくてもよい</param>
    /// <returns></returns>
    public bool TryConsumeBildMat(
    BildingMatType type,
    int amount,
    out int approvalValue)
    {
        return inventoryManager.TryConsumeBildMat(
            type,
            amount,
            out approvalValue);
    }

}

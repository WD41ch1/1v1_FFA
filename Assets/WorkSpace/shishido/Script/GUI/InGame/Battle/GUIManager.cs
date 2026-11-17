using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GUIManager : MonoBehaviour, IGuiInitialize
{
    public PlayerManager myPlayer;

    [SerializeField]
    private List<UIBase> UIList;

    public CanvasGroup canvasGroup { private set; get; }

    [Header("インベントリ入力")]
    [SerializeField]
    private string inventoryActionPath = "Player/Inventory";

    private InputAction inventoryAction;
    private InputActionAsset cachedActions;
    private string cachedActionPath;
    private bool inputWarningShown;

    private bool inventoryOpen;
    private bool initialized;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public void RegisterPlayer(PlayerManager player)
    {
        if (myPlayer != player && inventoryOpen)
            CloseInventory();

        myPlayer = player;

        inventoryAction = null;
        cachedActions = null;
        cachedActionPath = null;
        inputWarningShown = false;
    }

    public void Initialize()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInParent<CanvasGroup>();

        if (UIList != null)
        {
            foreach (UIBase ui in UIList)
            {
                if (ui == null)
                    continue;

                ui.Initialize(myPlayer, this);
            }
        }

        // UIの初期化が終わってから入力を受け付ける。
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        // 自分のPlayerに接続されたUIだけ操作する。
        if (myPlayer == null ||
            myPlayer.inputController == null ||
            !myPlayer.inputController.CanProcessInput)
        {
            if (inventoryOpen)
                CloseInventory();

            return;
        }

        if (!Application.isFocused)
            return;

        // PlayerInputControllerが使用しているPlayerInputを取得する。
        UnityEngine.InputSystem.PlayerInput playerInput =
            myPlayer.inputController.playerInput;

        if (playerInput == null ||
            !playerInput.isActiveAndEnabled ||
            playerInput.actions == null)
        {
            return;
        }

        // 実行中のPlayerが使用しているActionsから取得する。
        if (inventoryAction == null ||
            cachedActions != playerInput.actions ||
            cachedActionPath != inventoryActionPath)
        {
            cachedActions = playerInput.actions;
            cachedActionPath = inventoryActionPath;

            inventoryAction =
                string.IsNullOrWhiteSpace(inventoryActionPath)
                    ? null
                    : cachedActions.FindAction(
                        inventoryActionPath, false);
        }

        if (inventoryAction == null)
        {
            if (!inputWarningShown)
            {
                Debug.LogWarning(
                    $"[インベントリ入力] Input Actionsに" +
                    $"「{inventoryActionPath}」がありません。",
                    this);

                inputWarningShown = true;
            }

            return;
        }

        inputWarningShown = false;

        if (!inventoryAction.enabled)
            return;

        // Input Actionsに設定されたキーで開閉する。
        if (inventoryAction.WasPressedThisFrame())
        {
            SetInventoryOpen(!inventoryOpen);
        }
    }

    private void SetInventoryOpen(bool open)
    {
        if (inventoryOpen == open)
            return;

        if (open)
        {
            // 開く前のカーソル状態を保存する。
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
        }

        inventoryOpen = open;

        // CameraControllerへ開閉状態を伝える。
        CameraController.InventoryOpen = open;

        if (UIList != null)
        {
            foreach (UIBase ui in UIList)
            {
                if (ui == null)
                    continue;

                if (open)
                    ui.Show();
                else
                    ui.Hide();
            }
        }

        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreCursor();
        }
    }

    public void CloseInventory()
    {
        SetInventoryOpen(false);
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        // UI終了時にカメラ停止やカーソル解除を残さない。
        if (!inventoryOpen)
            return;

        inventoryOpen = false;
        CameraController.InventoryOpen = false;
        RestoreCursor();
    }
}
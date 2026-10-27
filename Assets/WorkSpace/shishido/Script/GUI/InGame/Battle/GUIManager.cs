using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GUIManager : MonoBehaviour, IGuiInitialize
{
    public PlayerManager myPlayer;

    [SerializeField]
    private List<UIBase> UIList;

    public CanvasGroup canvasGroup { private set; get; }

    private bool inventoryOpen;
    private bool initialized;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public void RegisterPlayer(PlayerManager player)
    {
        myPlayer = player;
    }

    public void Initialize()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInParent<CanvasGroup>();

        foreach (UIBase ui in UIList)
        {
            if (ui == null)
                continue;

            ui.Initialize(myPlayer, this);
        }

        // UIの初期化が終わってから入力を受け付ける。
        initialized = true;
    }

    private void Update()
    {
        if (!initialized || Keyboard.current == null)
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

        if (Keyboard.current.gKey.wasPressedThisFrame)
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

        foreach (UIBase ui in UIList)
        {
            if (ui == null)
                continue;

            if (open)
                ui.Show();
            else
                ui.Hide();
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
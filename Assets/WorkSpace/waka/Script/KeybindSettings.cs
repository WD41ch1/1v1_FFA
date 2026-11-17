using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeybindSettingsUI : MonoBehaviour
{
    // 起動中だけ保持する。ファイルには保存しない。
    private static string sessionOverrides;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        sessionOverrides = null;
    }

    private class Setting
    {
        public string label;
        public string actionPath;
        public int bindingIndex;

        public Setting(string label, string path, int index = 0)
        {
            this.label = label;
            actionPath = path;
            bindingIndex = index;
        }
    }

    private readonly Setting[] settings =
    {
        // Moveの0番は2DVector本体なので、1～4番を変更する。
        new Setting("前に移動", "Player/Move", 1),
        new Setting("後ろに移動", "Player/Move", 2),
        new Setting("左に移動", "Player/Move", 3),
        new Setting("右に移動", "Player/Move", 4),

        new Setting("ジャンプ", "Player/Jump"),
        new Setting("しゃがみ", "Player/Crouch"),
        new Setting("アイテムを拾う", "Player/PickUP"),
        new Setting("インベントリ", "Player/Inventory"),
        new Setting("ツルハシ", "Player/PickelSlot"),

        new Setting("射撃", "Combat/Fire"),
        new Setting("照準", "Combat/ADS"),
        new Setting("リロード", "Combat/Reload"),

        new Setting("建築を設置", "Building/Place"),
        new Setting("壁", "Building/Wall"),
        new Setting("階段", "Building/Ramp"),
        new Setting("床", "Building/Floor"),
        new Setting("屋根", "Building/Cone")
    };

    private PlayerInputController controller;
    private InputActionAsset actions;

    private InputActionRebindingExtensions.RebindingOperation operation;

    private bool isOpen;
    private bool restoring;
    private bool controllerWasEnabled;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private Vector2 scroll;
    private float nextSearchTime;
    private string message = "";
    private Coroutine restoreRoutine;

    private void Update()
    {
        // 設定中は保存済みのPlayerを使い続ける。
        if (isOpen || restoring)
        {
            if (controller == null)
            {
                CancelOperation();
                isOpen = false;
                restoring = false;
                RestoreCursor();
                actions = null;
            }

            return;
        }

        // ローカルPlayerの生成・再生成に対応する。
        if (Time.unscaledTime < nextSearchTime)
            return;

        nextSearchTime = Time.unscaledTime + 0.5f;
        FindLocalPlayer();
    }

    private void FindLocalPlayer()
    {
        PlayerInputController found = null;

        foreach (PlayerInputController candidate in
                 FindObjectsOfType<PlayerInputController>())
        {
            if (candidate != null && candidate.CanProcessInput)
            {
                found = candidate;
                break;
            }
        }

        if (found == null ||
            found.playerInput == null ||
            found.playerInput.actions == null)
        {
            controller = null;
            actions = null;
            return;
        }

        InputActionAsset nextActions = found.playerInput.actions;

        if (controller == found && actions == nextActions)
            return;

        controller = found;
        actions = nextActions;

        // 新しく生成されたローカルPlayerにも変更を反映する。
        if (!string.IsNullOrEmpty(sessionOverrides))
        {
            actions.LoadBindingOverridesFromJson(sessionOverrides);
        }
    }

    // UIボタンから呼ぶ。
    public void OpenSettings()
    {
        if (isOpen || restoring)
            return;

        FindLocalPlayer();

        if (controller == null || actions == null)
        {
            Debug.LogWarning(
                "キー設定：操作できるローカルPlayerがまだありません。",
                this);
            return;
        }

        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        controllerWasEnabled = controller.enabled;
        isOpen = true;
        message = "変更したい操作を選んでください。";

        // 既存のOnDisableで移動入力・射撃・各ActionMapを停止する。
        controller.enabled = false;
    }

    private void LateUpdate()
    {
        if (!isOpen && !restoring)
            return;

        // メニュー操作にマウスを使えるようにする。
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        float width = Mathf.Min(620f, Screen.width - 20f);
        float height = Mathf.Min(700f, Screen.height - 20f);

        Rect area = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("キー設定");
        GUILayout.Label(message);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && operation == null;

        scroll = GUILayout.BeginScrollView(scroll);

        foreach (Setting setting in settings)
        {
            InputAction action =
                actions != null
                    ? actions.FindAction(setting.actionPath, false)
                    : null;

            GUILayout.BeginHorizontal();

            GUILayout.Label(
                setting.label,
                GUILayout.Width(width * 0.4f));

            bool valid =
                action != null &&
                setting.bindingIndex < action.bindings.Count;

            string binding = valid
                ? action.GetBindingDisplayString(setting.bindingIndex)
                : "Actionがありません";

            if (GUILayout.Button(binding, GUILayout.Height(30f)) &&
                valid)
            {
                StartRebind(setting, action);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        if (GUILayout.Button("初期設定に戻す", GUILayout.Height(32f)))
        {
            actions.RemoveAllBindingOverrides();
            sessionOverrides = null;
            message = "初期設定に戻しました。";
        }

        GUI.enabled = previousEnabled;

        if (operation != null)
        {
            if (GUILayout.Button("変更をキャンセル", GUILayout.Height(32f)))
                CancelOperation();
        }
        else
        {
            if (GUILayout.Button("閉じる", GUILayout.Height(32f)))
                CloseSettings();
        }

        GUILayout.EndArea();
    }

    private void StartRebind(Setting setting, InputAction action)
    {
        if (operation != null)
            return;

        // リバインド対象は無効にしてから変更する。
        action.Disable();

        message =
            setting.label + "：新しいキーを押してください。Escでキャンセル。";

        operation = action
            .PerformInteractiveRebinding(setting.bindingIndex)
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("<Keyboard>/*")
            .WithControlsHavingToMatchPath("<Mouse>/*")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnCancel(op =>
            {
                operation = null;
                op.Dispose();

                message = "変更をキャンセルしました。";
            })
            .OnComplete(op =>
            {
                operation = null;
                op.Dispose();

                // JSONをメモリ上に保持するだけ。ディスク保存はしない。
                sessionOverrides = actions.SaveBindingOverridesAsJson();

                message =
                    setting.label + "を「" +
                    action.GetBindingDisplayString(setting.bindingIndex) +
                    "」に変更しました。";
            });

        operation.Start();
    }

    private void CancelOperation()
    {
        if (operation == null)
            return;

        // OnCancelでDisposeする。
        operation.Cancel();
    }

    public void CloseSettings()
    {
        if (!isOpen || restoring)
            return;

        CancelOperation();

        isOpen = false;
        restoring = true;

        restoreRoutine = StartCoroutine(RestoreAfterClose());
    }

    private IEnumerator RestoreAfterClose()
    {
        // 閉じるクリックが射撃などへ流れないように少し待つ。
        yield return new WaitForSecondsRealtime(0.15f);

        // 閉じるために押したマウスボタンを離すまで待つ。
        while (Mouse.current != null &&
               (Mouse.current.leftButton.isPressed ||
                Mouse.current.rightButton.isPressed))
        {
            yield return null;
        }

        RestorePlayerInput();
        RestoreCursor();

        restoring = false;
        restoreRoutine = null;
    }

    private void RestorePlayerInput()
    {
        if (controller != null && controllerWasEnabled)
        {
            // PlayerInputController自身が次のUpdateで
            // 所有権と建築モードを確認して入力を再開する。
            controller.enabled = true;
        }

        controllerWasEnabled = false;
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        CancelOperation();

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        if (isOpen || restoring)
        {
            RestorePlayerInput();
            RestoreCursor();
        }

        isOpen = false;
        restoring = false;
    }
}
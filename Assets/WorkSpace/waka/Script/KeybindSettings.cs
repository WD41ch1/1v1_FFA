using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeybindSettingsUI : MonoBehaviour
{
    // シーン移動しても設定画面を1つだけ維持する。
    private static KeybindSettingsUI instance;

    [Header("元のInput Actions")]
    [SerializeField]
    private InputActionAsset inputActions;

    [Header("作成したUIを登録")]
    [SerializeField]
    private GameObject settingsPanel;

    [SerializeField]
    private RectTransform bindList;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button resetButton;

    [SerializeField]
    private TMP_Text titleText;

    [Header("最初から表示する（通常はOFF）")]
    [SerializeField]
    private bool openOnStart = false;

    [Header("一覧の文字")]
    [SerializeField]
    private TMP_FontAsset font;

    private class Entry
    {
        public string label;
        public string path;
        public int index;
        public Button button;
        public TMP_Text text;

        public Entry(
            string label,
            string path,
            int index = 0)
        {
            this.label = label;
            this.path = path;
            this.index = index;
        }
    }

    private readonly List<Entry> entries = new List<Entry>
    {
        // 0番は2DVector本体。方向キーは1～4番。
        new Entry("Move Forward", "Player/Move", 1),
        new Entry("Move Back", "Player/Move", 2),
        new Entry("Move Left", "Player/Move", 3),
        new Entry("Move Right", "Player/Move", 4),

        new Entry("Jump", "Player/Jump"),
        new Entry("Crouch", "Player/Crouch"),
        new Entry("Pick Up", "Player/PickUP"),
        new Entry("Inventory", "Player/Inventory"),
        new Entry("Pickaxe", "Player/PickelSlot"),

        new Entry("Slot 1", "Player/Slot1"),
        new Entry("Slot 2", "Player/Slot2"),
        new Entry("Slot 3", "Player/Slot3"),
        new Entry("Slot 4", "Player/Slot4"),
        new Entry("Slot 5", "Player/Slot5"),

        new Entry("Fire", "Combat/Fire"),
        new Entry("Aim", "Combat/ADS"),
        new Entry("Reload", "Combat/Reload"),

        new Entry("Place Build", "Building/Place"),
        new Entry("Wall", "Building/Wall"),
        new Entry("Ramp", "Building/Ramp"),
        new Entry("Floor", "Building/Floor"),
        new Entry("Roof", "Building/Cone"),

        // 建築編集キー。
        new Entry("Edit", "Player/Edit")
    };

    private InputActionAsset workingActions;

    private InputActionRebindingExtensions.RebindingOperation operation;

    private bool ready;
    private bool isOpen;

    // Escによるリバインドキャンセルと画面開閉の重複を防ぐ。
    private int escapeConsumedFrame = -1;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
    }

    private void Awake()
    {
        // シーン移動先にある重複Canvasは削除する。
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        if (inputActions == null ||
            settingsPanel == null ||
            bindList == null ||
            closeButton == null ||
            resetButton == null)
        {
            Debug.LogError(
                "KeybindSettingsUI：Inspectorの参照を設定してください。",
                this);

            return;
        }

        // Canvas本体を非表示にするとEscを受け取れなくなる。
        if (settingsPanel == gameObject)
        {
            Debug.LogError(
                "Settings PanelにはCanvas本体ではなく、" +
                "子のSettingPanelを登録してください。",
                this);

            return;
        }

        instance = this;

        // 親ごと別のオブジェクトを残さないように独立させる。
        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject);

        // 元アセットを変更しないよう、実行時コピーを使う。
        workingActions = Instantiate(inputActions);
        workingActions.Disable();

        KeybindSession.Apply(workingActions);

        CreateButtons();

        closeButton.onClick.AddListener(CloseSettings);
        resetButton.onClick.AddListener(ResetSettings);

        ready = true;
        settingsPanel.SetActive(false);

        SetTitle("Key Settings");
        RefreshLabels();
    }

    private void Start()
    {
        if (ready && openOnStart)
            OpenSettings();
    }

    private void Update()
    {
        if (!ready || !Application.isFocused)
            return;

        if (Keyboard.current == null)
            return;

        if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        // 変更待機中はリバインド側がEscを処理する。
        // 同じフレームに画面まで閉じないようにする。
        if (operation != null ||
            escapeConsumedFrame == Time.frameCount)
        {
            return;
        }

        if (isOpen)
            CloseSettings();
        else
            OpenSettings();
    }

    private void LateUpdate()
    {
        if (!isOpen)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CreateButtons()
    {
        GridLayoutGroup grid =
            bindList.GetComponent<GridLayoutGroup>();

        if (grid == null)
        {
            grid =
                bindList.gameObject.AddComponent<GridLayoutGroup>();
        }

        grid.constraint =
            GridLayoutGroup.Constraint.FixedColumnCount;

        grid.constraintCount = 2;
        grid.cellSize = new Vector2(430f, 48f);
        grid.spacing = new Vector2(20f, 10f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;

        foreach (Entry entry in entries)
        {
            GameObject buttonObject = new GameObject(
                entry.label,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

            buttonObject.transform.SetParent(bindList, false);

            Image background =
                buttonObject.GetComponent<Image>();

            background.color =
                new Color(0.22f, 0.28f, 0.36f, 1f);

            Button button =
                buttonObject.GetComponent<Button>();

            button.targetGraphic = background;

            // リバインド中のキーでUI選択が移動しないようにする。
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            GameObject textObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.transform.SetParent(
                buttonObject.transform,
                false);

            RectTransform rect =
                textObject.GetComponent<RectTransform>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 3f);
            rect.offsetMax = new Vector2(-10f, -3f);

            TextMeshProUGUI text =
                textObject.GetComponent<TextMeshProUGUI>();

            if (font != null)
                text.font = font;

            text.fontSize = 22f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            entry.button = button;
            entry.text = text;

            Entry capturedEntry = entry;

            button.onClick.AddListener(
                () => StartRebind(capturedEntry));
        }
    }

    public void OpenSettings()
    {
        if (!ready || isOpen || !isActiveAndEnabled)
            return;

        KeybindSession.Apply(workingActions);

        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        isOpen = true;

        KeybindSession.SetSettingsOpen(true);

        settingsPanel.SetActive(true);

        SetTitle("Key Settings");
        RefreshLabels();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSettings()
    {
        if (!isOpen)
            return;

        CancelRebind();

        isOpen = false;

        KeybindSession.SetSettingsOpen(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        RestoreCursor();
    }

    private void StartRebind(Entry entry)
    {
        if (!ready || !isOpen || operation != null)
            return;

        InputAction action =
            workingActions.FindAction(entry.path, false);

        if (action == null ||
            entry.index < 0 ||
            entry.index >= action.bindings.Count)
        {
            SetTitle("Action / Binding not found");
            return;
        }

        action.Disable();

        SetTitle("Press a key / Esc: Cancel");

        operation = action
            .PerformInteractiveRebinding(entry.index)
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
                // キャンセルに使ったEscでは画面を閉じない。
                escapeConsumedFrame = Time.frameCount;

                operation = null;
                op.Dispose();

                SetTitle("Cancelled / Esc: Close");
                RefreshLabels();
            })
            .OnComplete(op =>
            {
                operation = null;
                op.Dispose();

                UpdateExitBuildingBinding(entry, action);

                // 起動中のメモリにだけ保持する。
                KeybindSession.Store(workingActions);

                SetTitle("Key Settings");
                RefreshLabels();
            });

        RefreshLabels();
        operation.Start();
    }

    private void UpdateExitBuildingBinding(
        Entry entry,
        InputAction changedAction)
    {
        int slotIndex = -1;

        switch (entry.path)
        {
            case "Player/Slot1":
                slotIndex = 0;
                break;

            case "Player/Slot2":
                slotIndex = 1;
                break;

            case "Player/Slot3":
                slotIndex = 2;
                break;

            case "Player/Slot4":
                slotIndex = 3;
                break;

            case "Player/Slot5":
                slotIndex = 4;
                break;
        }

        if (slotIndex < 0)
            return;

        InputAction exitAction =
            workingActions.FindAction(
                "Player/ExitBuilding",
                false);

        if (exitAction == null ||
            slotIndex >= exitAction.bindings.Count)
        {
            return;
        }

        // スロット変更と同じキーで建築モードを終了できるようにする。
        exitAction.ApplyBindingOverride(
            slotIndex,
            changedAction.bindings[entry.index].effectivePath);
    }

    public void ResetSettings()
    {
        if (!ready || operation != null)
            return;

        workingActions.RemoveAllBindingOverrides();

        KeybindSession.Store(workingActions);

        SetTitle("Default keys restored");
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (workingActions == null)
            return;

        bool waiting = operation != null;

        foreach (Entry entry in entries)
        {
            if (entry.button == null || entry.text == null)
                continue;

            InputAction action =
                workingActions.FindAction(entry.path, false);

            bool valid =
                action != null &&
                entry.index >= 0 &&
                entry.index < action.bindings.Count;

            entry.button.interactable = valid && !waiting;

            string keyName = valid
                ? action.GetBindingDisplayString(entry.index)
                : "Missing";

            entry.text.text =
                entry.label + " : " + keyName;
        }

        if (resetButton != null)
            resetButton.interactable = !waiting;

        if (closeButton != null)
            closeButton.interactable = !waiting;
    }

    private void SetTitle(string text)
    {
        if (titleText != null)
            titleText.text = text;
    }

    private void CancelRebind()
    {
        if (operation != null)
            operation.Cancel();
    }

    private void RestoreCursor()
    {
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        CancelRebind();

        if (!isOpen)
            return;

        isOpen = false;

        KeybindSession.SetSettingsOpen(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        RestoreCursor();
    }

    private void OnDestroy()
    {
        CancelRebind();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseSettings);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetSettings);

        if (workingActions != null)
            Destroy(workingActions);

        if (instance == this)
            instance = null;
    }
}
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using FishNet.Object;
using Cysharp.Threading.Tasks;
using static GameConst;

[DefaultExecutionOrder(-200)]
public class PlayerInputController : MonoBehaviour
{
    public PlayerManager owner;

    [Header("入力と建築管理")]
    [Tooltip("同じPlayerのPlayer Inputコンポーネント")]
    public UnityEngine.InputSystem.PlayerInput playerInput;

    [Header("インベントリ")]
    public UnityEvent onInventoryToggle = new UnityEvent();

    public BuildingSystem buildingSystem { get; private set; }

    private EditSystem editSystem;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;

    public Vector2 MoveInput =>
        CanProcessGameplay ? moveInput : Vector2.zero;

    public Vector2 LookInput =>
        CanProcessGameplay ? lookInput : Vector2.zero;

    public bool JumpPressed =>
        CanProcessGameplay && jumpPressed;

    public bool BuildWallPressed { get; private set; }
    public bool BuildRampPressed { get; private set; }
    public bool BuildFloorPressed { get; private set; }
    public bool BuildConePressed { get; private set; }
    public bool isADS { get; private set; }

    private InputActionMap playerMap;
    private InputActionMap combatMap;
    private InputActionMap buildingMap;

    private InputAction buildAction;
    private InputAction wallAction;
    private InputAction rampAction;
    private InputAction floorAction;
    private InputAction coneAction;

    private int exitFrame = -1;

    private bool combatEnabled;
    private bool started;
    private bool localInputActive;
    private bool mapErrorReported;
    private bool gameplayWasBlocked;
    private bool wasEditing;

    private NetworkObject playerNetworkObject;

    private bool IsLocalOwner =>
        playerNetworkObject != null &&
        playerNetworkObject.IsClientInitialized &&
        playerNetworkObject.IsOwner;

    // インベントリを閉じる操作も受け付けるため、
    // この判定にはUI表示状態を含めない。
    public bool CanProcessInput =>
        isActiveAndEnabled &&
        localInputActive &&
        IsLocalOwner;

    private bool IsMenuOpen =>
        CameraController.InventoryOpen ||
        KeybindSession.SettingsOpen;

    private bool IsGameplayBlocked =>
        !Application.isFocused ||
        CameraController.InventoryOpen ||
        KeybindSession.BlocksGameplay;

    private bool CanProcessGameplay =>
        CanProcessInput && !IsGameplayBlocked;

    private bool IsEditingBuilding =>
        editSystem != null && editSystem.IsEditing;

    private bool CanProcessWeaponInput =>
        CanProcessGameplay && !IsEditingBuilding;

    public bool BuildHeld =>
        CanProcessGameplay &&
        !IsEditingBuilding &&
        buildingMap != null &&
        buildingMap.enabled &&
        buildAction != null &&
        buildAction.enabled &&
        buildAction.IsPressed();

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<PlayerManager>();

        playerNetworkObject = GetComponent<NetworkObject>();

        buildingSystem =
            GetComponentInChildren<BuildingSystem>(true);

        editSystem =
            GetComponentInChildren<EditSystem>(true);

        if (playerNetworkObject == null)
        {
            Debug.LogError(
                "PlayerInputController：同じPlayerにNetworkObjectが必要です。",
                this);
        }

        if (playerInput == null)
        {
            playerInput =
                GetComponent<UnityEngine.InputSystem.PlayerInput>();
        }

        if (playerInput != null)
            playerInput.enabled = false;
    }

    private void Start()
    {
        started = true;

        if (buildingSystem == null)
        {
            Debug.LogWarning(
                "PlayerInputController：Player配下にBuildingSystemを配置してください。",
                this);
        }
    }

    private bool ResolveMaps()
    {
        if (playerInput == null || playerInput.actions == null)
            return false;

        playerMap =
            playerInput.actions.FindActionMap("Player", false);

        combatMap =
            playerInput.actions.FindActionMap("Combat", false);

        buildingMap =
            playerInput.actions.FindActionMap("Building", false);

        buildAction = buildingMap?.FindAction("Place", false);
        wallAction = buildingMap?.FindAction("Wall", false);
        rampAction = buildingMap?.FindAction("Ramp", false);
        floorAction = buildingMap?.FindAction("Floor", false);
        coneAction = buildingMap?.FindAction("Cone", false);

        return
            playerMap != null &&
            combatMap != null &&
            buildingMap != null &&
            buildAction != null &&
            wallAction != null &&
            rampAction != null &&
            floorAction != null &&
            coneAction != null;
    }

    private void Update()
    {
        if (!started || playerInput == null)
            return;

        if (!IsLocalOwner)
        {
            if (localInputActive)
                StopLocalInput();

            if (playerInput.enabled)
                playerInput.enabled = false;

            return;
        }

        if (!localInputActive)
        {
            if (mapErrorReported)
                return;

            playerInput.enabled = true;

            if (!ResolveMaps())
            {
                playerInput.enabled = false;
                mapErrorReported = true;

                Debug.LogError(
                    "PlayerInputController：" +
                    "Player / Combat / Buildingと、" +
                    "Place / Wall / Ramp / Floor / Coneを確認してください。",
                    this);

                return;
            }

            localInputActive = true;
            RestoreInputMode();
        }

        UpdateGameplayBlocking();
        UpdateEditingBlocking();
    }

    private void LateUpdate()
    {
        if (!IsLocalOwner)
            return;

        // EditSystemやGUIManagerのUpdate後の状態も確認する。
        UpdateGameplayBlocking();
        UpdateEditingBlocking();

        bool showCursor =
            IsMenuOpen ||
            !Application.isFocused ||
            !CanProcessInput;

        SetCursorVisible(showCursor);
    }

    private void UpdateGameplayBlocking()
    {
        if (!localInputActive)
            return;

        bool blocked = IsGameplayBlocked;

        if (blocked)
        {
            ResetJump();
            ResetBuildInputs();

            if (!gameplayWasBlocked)
                StopCombat();
        }

        gameplayWasBlocked = blocked;
    }

    private void UpdateEditingBlocking()
    {
        if (!localInputActive)
            return;

        bool editing = IsEditingBuilding;

        if (editing)
        {
            ResetBuildInputs();

            // 編集開始時に連射予約とADSを解除する。
            if (!wasEditing)
                StopCombat();
        }

        wasEditing = editing;
    }

    private void SetCursorVisible(bool visible)
    {
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = visible;
    }

    private void OnEnable()
    {
        if (CanProcessInput && ResolveMaps())
            RestoreInputMode();
    }

    private void OnDisable()
    {
        bool controlledLocalPlayer =
            localInputActive || IsLocalOwner;

        StopLocalInput();

        if (controlledLocalPlayer)
            SetCursorVisible(true);
    }

    private void RestoreInputMode()
    {
        playerMap.Enable();

        bool building =
            buildingSystem != null &&
            buildingSystem.GetCurrentBuildType() !=
            BuildingSystem.BuildType.None;

        SetBuildingMode(building);
    }

    public void RegisterPlayer(PlayerManager player)
    {
        owner = player;
    }

    // ---------------------------
    // インベントリ
    // ---------------------------

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (!CanProcessInput ||
            !context.performed ||
            !Application.isFocused ||
            KeybindSession.BlocksGameplay)
        {
            return;
        }

        onInventoryToggle.Invoke();
    }

    // ---------------------------
    // 移動・視点・ジャンプ
    // ---------------------------

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!CanProcessInput)
            return;

        // UI中にキーを離した状態も記録する。
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!CanProcessInput)
            return;

        lookInput = CanProcessGameplay
            ? context.ReadValue<Vector2>()
            : Vector2.zero;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay || !context.performed)
            return;

        jumpPressed = true;
    }

    public void OnPickUP(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed)
        {
            return;
        }

        if (owner != null && owner.cameraController != null)
            owner.cameraController.StandbyPickUpItem();
    }

    // ---------------------------
    // 装備スロット
    // ---------------------------

    private void ExitBuildingForEquipment()
    {
        exitFrame = Time.frameCount;

        ResetBuildInputs();

        if (buildingSystem != null)
            buildingSystem.CancelBuild();
        else
            SetBuildingMode(false);
    }

    private void EquipSlot(
        InputAction.CallbackContext context,
        int slot)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed)
        {
            return;
        }

        if (owner == null || owner.equipmentManager == null)
            return;

        StopCombat();
        ExitBuildingForEquipment();

        owner.equipmentManager.Equip(slot).Forget();
    }

    public void OnEquipPickelSlot(InputAction.CallbackContext context)
    {
        EquipSlot(context, PICKEL_SLOT);
    }

    public void OnEquipItemSlot1(InputAction.CallbackContext context)
    {
        EquipSlot(context, ITEM_SLOT_1);
    }

    public void OnEquipItemSlot2(InputAction.CallbackContext context)
    {
        EquipSlot(context, ITEM_SLOT_2);
    }

    public void OnEquipItemSlot3(InputAction.CallbackContext context)
    {
        EquipSlot(context, ITEM_SLOT_3);
    }

    public void OnEquipItemSlot4(InputAction.CallbackContext context)
    {
        EquipSlot(context, ITEM_SLOT_4);
    }

    public void OnEquipItemSlot5(InputAction.CallbackContext context)
    {
        EquipSlot(context, ITEM_SLOT_5);
    }

    public void OnNextSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed)
        {
            return;
        }

        if (owner == null || owner.equipmentManager == null)
            return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y >= 0f)
            return;

        StopCombat();
        ExitBuildingForEquipment();

        owner.equipmentManager.NextItemEquip();
    }

    public void OnPreviousSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed)
        {
            return;
        }

        if (owner == null || owner.equipmentManager == null)
            return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y <= 0f)
            return;

        StopCombat();
        ExitBuildingForEquipment();

        owner.equipmentManager.PreviousItemEquip();
    }

    // ---------------------------
    // 建築モード
    // ---------------------------

    public void SetCombatEnabled(bool enabled)
    {
        SetBuildingMode(!enabled);
    }

    public void SetBuildingMode(bool building)
    {
        if (!CanProcessInput || !ResolveMaps())
            return;

        playerMap.Enable();

        wallAction.Enable();
        rampAction.Enable();
        floorAction.Enable();
        coneAction.Enable();

        if (building)
        {
            combatEnabled = false;
            StopCombat();

            combatMap.Disable();
            buildAction.Enable();
        }
        else
        {
            buildAction.Disable();
            ResetBuildInputs();

            combatEnabled = true;
            combatMap.Enable();
        }
    }

    public void OnExitBuilding(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed)
        {
            return;
        }

        ExitBuildingForEquipment();
    }

    private bool CanSelectBuilding(InputAction.CallbackContext context)
    {
        if (!CanProcessGameplay ||
            IsEditingBuilding ||
            !context.performed ||
            exitFrame == Time.frameCount)
        {
            return false;
        }

        if (!ResolveMaps())
            return false;

        if (context.action == null ||
            context.action.actionMap != buildingMap)
        {
            return false;
        }

        if (buildingSystem == null ||
            !buildingSystem.isActiveAndEnabled ||
            !buildingSystem.CanProcessInput)
        {
            Debug.LogWarning(
                "PlayerInputController：有効なBuildingSystemを設定してください。",
                this);

            return false;
        }

        SetBuildingMode(true);
        return true;
    }

    public void OnBuildWall(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context))
            return;

        BuildWallPressed = true;
    }

    public void OnBuildRamp(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context))
            return;

        BuildRampPressed = true;
    }

    public void OnBuildFloor(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context))
            return;

        BuildFloorPressed = true;
    }

    public void OnBuildCone(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context))
            return;

        BuildConePressed = true;
    }

    // ---------------------------
    // 武器操作
    // ---------------------------

    private bool CanUseCombat(InputAction.CallbackContext context)
    {
        return
            CanProcessWeaponInput &&
            combatEnabled &&
            context.action != null &&
            context.action.actionMap == combatMap &&
            owner != null &&
            owner.equipmentManager != null;
    }

    public void OnUsePrimary(InputAction.CallbackContext context)
    {
        // 編集中・UI中でも、射撃終了の通知は処理する。
        if (context.canceled)
        {
            if (IsLocalOwner &&
                owner != null &&
                owner.equipmentManager != null)
            {
                owner.equipmentManager
                    .GetcurrentItem()?.UsePrimary(context);
            }

            return;
        }

        if (!CanUseCombat(context))
            return;

        owner.equipmentManager.GetcurrentItem()?.UsePrimary(context);
    }

    public void OnUseSecondary(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            bool wasADS = isADS;
            isADS = false;

            if (wasADS &&
                IsLocalOwner &&
                owner != null &&
                owner.equipmentManager != null)
            {
                owner.equipmentManager
                    .GetcurrentItem()?.UseSecondary(false);
            }

            return;
        }

        if (!CanUseCombat(context) || !context.started)
            return;

        isADS = true;

        owner.equipmentManager.GetcurrentItem()?.UseSecondary(true);
    }

    public void OnCombatReload(InputAction.CallbackContext context)
    {
        if (!CanUseCombat(context) || !context.performed)
            return;

        OnUseReload();
    }

    public void OnUseReload()
    {
        if (!CanProcessWeaponInput ||
            !combatEnabled ||
            owner == null ||
            owner.equipmentManager == null)
        {
            return;
        }

        owner.equipmentManager.GetcurrentItem()?.UseReload();
    }

    private void StopCombat()
    {
        bool wasADS = isADS;
        isADS = false;

        if (owner == null || owner.equipmentManager == null)
            return;

        var item = owner.equipmentManager.GetcurrentItem();

        if (item is WeaponBase weapon)
            weapon.StopFire();

        if (wasADS && item != null)
            item.UseSecondary(false);
    }

    // ---------------------------
    // 入力状態のリセット
    // ---------------------------

    public void ResetJump()
    {
        jumpPressed = false;
    }

    public void ResetBuildWall()
    {
        BuildWallPressed = false;
    }

    public void ResetBuildRamp()
    {
        BuildRampPressed = false;
    }

    public void ResetBuildFloor()
    {
        BuildFloorPressed = false;
    }

    public void ResetBuildCone()
    {
        BuildConePressed = false;
    }

    private void ResetBuildInputs()
    {
        ResetBuildWall();
        ResetBuildRamp();
        ResetBuildFloor();
        ResetBuildCone();
    }

    private void StopLocalInput()
    {
        localInputActive = false;

        if (playerInput != null)
            playerInput.enabled = false;

        combatEnabled = false;
        StopCombat();

        combatMap?.Disable();
        buildingMap?.Disable();
        playerMap?.Disable();

        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        gameplayWasBlocked = false;
        wasEditing = false;

        ResetJump();
        ResetBuildInputs();
    }
}
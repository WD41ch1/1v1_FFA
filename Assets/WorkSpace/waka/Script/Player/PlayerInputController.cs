using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using FishNet.Object;
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

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool JumpPressed { get; private set; }
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

    private NetworkObject playerNetworkObject;

    public bool CanProcessInput =>
        isActiveAndEnabled &&
        localInputActive &&
        playerNetworkObject != null &&
        playerNetworkObject.IsClientInitialized &&
        playerNetworkObject.IsOwner;

    public bool BuildHeld =>
        CanProcessInput &&
        buildingMap != null &&
        buildingMap.enabled &&
        buildAction != null &&
        buildAction.enabled &&
        buildAction.IsPressed();

    private void Awake()
    {
        owner = GetComponent<PlayerManager>();
        playerNetworkObject = GetComponent<NetworkObject>();

        buildingSystem =
            GetComponentInChildren<BuildingSystem>(true);

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

        buildAction = buildingMap != null
            ? buildingMap.FindAction("Place", false)
            : null;

        wallAction = buildingMap != null
            ? buildingMap.FindAction("Wall", false)
            : null;

        rampAction = buildingMap != null
            ? buildingMap.FindAction("Ramp", false)
            : null;

        floorAction = buildingMap != null
            ? buildingMap.FindAction("Floor", false)
            : null;

        coneAction = buildingMap != null
            ? buildingMap.FindAction("Cone", false)
            : null;

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

        bool localOwner =
            playerNetworkObject != null &&
            playerNetworkObject.IsClientInitialized &&
            playerNetworkObject.IsOwner;

        if (!localOwner)
        {
            if (localInputActive)
                StopLocalInput();

            if (playerInput.enabled)
                playerInput.enabled = false;

            return;
        }

        if (localInputActive || mapErrorReported)
            return;

        playerInput.enabled = true;

        if (!ResolveMaps())
        {
            playerInput.enabled = false;

            if (!mapErrorReported)
            {
                Debug.LogError(
                    "PlayerInputController：" +
                    "Player / Combat / Buildingと、" +
                    "Place / Wall / Ramp / Floor / Coneを確認してください。",
                    this);
            }

            mapErrorReported = true;
            return;
        }

        localInputActive = true;
        RestoreInputMode();
    }

    private void OnEnable()
    {
        if (CanProcessInput && ResolveMaps())
            RestoreInputMode();
    }

    private void OnDisable()
    {
        StopLocalInput();
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
        if (!context.performed)
            return;

        Debug.Log(
            $"[インベントリ入力] Gを受信 / 入力可能={CanProcessInput}",
            this);

        if (!CanProcessInput)
            return;

        onInventoryToggle.Invoke();
    }

    // ---------------------------
    // 移動・視点・ジャンプ
    // ---------------------------

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!CanProcessInput)
            return;

        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if (!CanProcessInput)
            return;

        LookInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !context.performed)
            return;

        JumpPressed = true;
    }

    public void OnPickUP(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !context.performed)
            return;

        if (owner != null && owner.cameraController != null)
            owner.cameraController.StandbyPickUpItem();
    }

    // ---------------------------
    // 装備スロット
    // ---------------------------

    private void EquipSlot(
        InputAction.CallbackContext context,
        int slot)
    {
        if (!CanProcessInput || !context.performed)
            return;

        if (owner == null || owner.equipmentManager == null)
            return;

        owner.equipmentManager.Equip(owner, slot);
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
        if (!CanProcessInput || !context.performed)
            return;

        if (owner == null || owner.equipmentManager == null)
            return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y < 0f)
            owner.equipmentManager.NextItemEquip(owner);
    }

    public void OnPreviousSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !context.performed)
            return;

        if (owner == null || owner.equipmentManager == null)
            return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y > 0f)
            owner.equipmentManager.PreviousItemEquip(owner);
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

        // InventoryもこのMapに置くので常時有効にする。
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

            ResetBuildWall();
            ResetBuildRamp();
            ResetBuildFloor();
            ResetBuildCone();

            combatEnabled = true;
            combatMap.Enable();
        }
    }

    public void OnExitBuilding(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !context.performed)
            return;

        exitFrame = Time.frameCount;

        if (buildingSystem != null)
            buildingSystem.CancelBuild();
        else
            SetBuildingMode(false);
    }

    private bool CanSelectBuilding(InputAction.CallbackContext context)
    {
        if (!context.performed || exitFrame == Time.frameCount)
            return false;

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
        if (!CanProcessInput || !CanSelectBuilding(context))
            return;

        BuildWallPressed = true;
    }

    public void OnBuildRamp(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !CanSelectBuilding(context))
            return;

        BuildRampPressed = true;
    }

    public void OnBuildFloor(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !CanSelectBuilding(context))
            return;

        BuildFloorPressed = true;
    }

    public void OnBuildCone(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !CanSelectBuilding(context))
            return;

        BuildConePressed = true;
    }

    // ---------------------------
    // 武器操作
    // ---------------------------

    private bool CanUseCombat(InputAction.CallbackContext context)
    {
        return
            combatEnabled &&
            context.action != null &&
            context.action.actionMap == combatMap &&
            owner != null &&
            owner.equipmentManager != null;
    }

    public void OnUsePrimary(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !CanUseCombat(context))
            return;

        owner.equipmentManager.GetcurrentItem()?.UsePrimary(context);
    }

    public void OnUseSecondary(InputAction.CallbackContext context)
    {
        if (!CanProcessInput || !CanUseCombat(context))
            return;

        if (context.started)
            isADS = true;
        else if (context.canceled)
            isADS = false;
        else
            return;

        owner.equipmentManager.GetcurrentItem()?.UseSecondary(isADS);
    }

    public void OnCombatReload(InputAction.CallbackContext context)
    {
        if (!CanProcessInput ||
            !CanUseCombat(context) ||
            !context.performed)
        {
            return;
        }

        OnUseReload();
    }

    public void OnUseReload()
    {
        if (!CanProcessInput ||
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
        JumpPressed = false;
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

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;

        ResetJump();
        ResetBuildWall();
        ResetBuildRamp();
        ResetBuildFloor();
        ResetBuildCone();
    }
}
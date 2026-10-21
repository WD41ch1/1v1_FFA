using UnityEngine;
using FishNet.Object;
using UnityEngine.InputSystem;
using static GameConst;

[DefaultExecutionOrder(-200)]
public class PlayerInputController : MonoBehaviour
{
    public PlayerManager owner;

    [Header("入力と建築管理")]
    [Tooltip("同じPlayerに付いたUnity標準のPlayer Inputコンポーネント")]
    public UnityEngine.InputSystem.PlayerInput playerInput;
    public BuildingSystem buildingSystem { get; private set; }
    // BuildingSystemが設定されていない場合は、同じPlayerに付いたBuildingSystemを探す。
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
    private InputAction wallAction, rampAction, floorAction, coneAction;
    private int exitFrame = -1;

    // Building/Placeの長押し状態を建築管理が読む。
    public bool BuildHeld => CanProcessInput && buildingMap != null && buildingMap.enabled &&
        buildAction != null && buildAction.enabled && buildAction.IsPressed();
    private bool combatEnabled;
    private bool started;
    private NetworkObject playerNetworkObject;
    private bool localInputActive;
    private bool mapErrorReported;
    public bool CanProcessInput => isActiveAndEnabled && localInputActive &&
        playerNetworkObject != null && playerNetworkObject.IsClientInitialized &&
        playerNetworkObject.IsOwner;

    private void Awake()
    {
        // PlayerInputが設定されていない場合は、同じGameObjectに付いたPlayerInputを探す。
        owner = GetComponent<PlayerManager>();
        playerNetworkObject = GetComponent<NetworkObject>();
        buildingSystem = GetComponentInChildren<BuildingSystem>(true);
        if (playerNetworkObject == null)
            Debug.LogError("PlayerInputController: 同じPlayerにNetworkObjectが必要です。", this);
        if (playerInput == null)
            playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;
    }
    // PlayerInputのActionMapとActionを解決する。PlayerInputが無効化されている場合は失敗する。
    private bool ResolveMaps()
    {
        if (playerInput == null || playerInput.actions == null) return false;
        // PlayerInputが実際に使用するActionsを参照する。
        playerMap = playerInput.actions.FindActionMap("Player", false);
        combatMap = playerInput.actions.FindActionMap("Combat", false);
        buildingMap = playerInput.actions.FindActionMap("Building", false);
        buildAction = buildingMap != null ? buildingMap.FindAction("Place", false) : null;
        wallAction = buildingMap != null ? buildingMap.FindAction("Wall", false) : null;
        rampAction = buildingMap != null ? buildingMap.FindAction("Ramp", false) : null;
        floorAction = buildingMap != null ? buildingMap.FindAction("Floor", false) : null;
        coneAction = buildingMap != null ? buildingMap.FindAction("Cone", false) : null;
        return playerMap != null && combatMap != null &&
            buildingMap != null && buildAction != null && wallAction != null &&
            rampAction != null && floorAction != null && coneAction != null;
    }

    private void Start()
    {
        started = true;
        if (buildingSystem == null)
            Debug.LogWarning("PlayerInputController: Player配下にBuildingSystemを配置してください。", this);
    }

    // StartとSpawnの順序に依存せず、所有権が確定してから入力開始。
    private void Update()
    {
        if (!started || playerInput == null) return;
        bool localOwner = playerNetworkObject != null &&
            playerNetworkObject.IsClientInitialized && playerNetworkObject.IsOwner;
        if (!localOwner)
        {
            if (localInputActive) StopLocalInput();
            if (playerInput.enabled) playerInput.enabled = false;
            return;
        }
        if (localInputActive || mapErrorReported) return;
        playerInput.enabled = true;
        if (!ResolveMaps())
        {
            playerInput.enabled = false;
            if (!mapErrorReported)
                Debug.LogError("PlayerInputController: Player / Combat / BuildingとPlace / Wall / Ramp / Floor / Coneを確認してください。", this);
            mapErrorReported = true;
            return;
        }
        localInputActive = true;
        RestoreInputMode();
    }
    private void OnEnable()
    {
        // Startより前にOnEnableが呼ばれる場合があるので、ResolveMapsが成功していればRestoreInputModeする。
        if (CanProcessInput && ResolveMaps()) RestoreInputMode();
    }

    private void RestoreInputMode()
    {
        // PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
        playerMap.Enable();
        bool building = buildingSystem != null &&
            buildingSystem.GetCurrentBuildType() != BuildingSystem.BuildType.None;
        SetBuildingMode(building);
    }

    // CombatのPrimary / Secondary / Reloadは、PlayerInputControllerがPlayerManagerに登録されている場合にのみ有効。
    public void SetCombatEnabled(bool enabled)
    {
        SetBuildingMode(!enabled);
    }

    // 建築モードの切り替え。建築モードの入口は常時有効。
    public void SetBuildingMode(bool building)
    {
        if (!CanProcessInput || !ResolveMaps()) return;
        playerMap.Enable();
        // モードへの入口は常時有効。Map全体をEnableするとPlaceまで有効になるため個別に操作。
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

    // Player/ExitBuildingに数字1～5と左Shiftを割り当てる。
    public void OnExitBuilding(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!context.performed) return;
        exitFrame = Time.frameCount;
        if (buildingSystem != null) buildingSystem.CancelBuild();
        else SetBuildingMode(false);
    }

    // BuildingのWall / Ramp / Floor / Coneは、建築モードへの入口も兼ねる。
    private bool CanSelectBuilding(InputAction.CallbackContext context)
    {
        // 建築モードへの入口は常時有効。
        if (!context.performed || exitFrame == Time.frameCount) return false;
        if (!ResolveMaps()) return false;
        if (context.action == null || context.action.actionMap != buildingMap) return false;
        if (buildingSystem == null || !buildingSystem.isActiveAndEnabled || !buildingSystem.CanProcessInput)
        {
            Debug.LogWarning("PlayerInputController: 有効なBuilding Systemを設定してください。", this);
            return false;
        }
        // BuildingSystem.Updateを待たずに連射・ADSを止める。
        SetBuildingMode(true);
        return true;
    }

    // CombatのPrimary / Secondary / Reloadは、PlayerInputControllerがPlayerManagerに登録されている場合にのみ有効。
    private void StopCombat()
    {
        bool wasADS = isADS;
        isADS = false;
        if (owner == null || owner.equipmentManager == null) return;
        var item = owner.equipmentManager.GetcurrentItem();
        WeaponBase weapon = item as WeaponBase;
        if (weapon != null) weapon.StopFire();
        if (wasADS && item != null) item.UseSecondary(false);
    }

    // CombatのPrimary / Secondary / Reloadは、PlayerInputControllerがPlayerManagerに登録されている場合にのみ有効。
    private bool CanUseCombat(InputAction.CallbackContext context)
    {
        // Player側に古いFireイベントが残っていても発射させない。
        return combatEnabled && context.action != null &&
            context.action.actionMap == combatMap &&
            owner != null && owner.equipmentManager != null;
    }

    // PlayerInputのMoveイベントにはこちらを登録する。
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        MoveInput = context.ReadValue<Vector2>();
        Debug.Log("Move : " + MoveInput);
    }

    // PlayerInputのLookイベントにはこちらを登録する。
    public void OnLook(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        LookInput = context.ReadValue<Vector2>();
    }

    // PlayerInputのJumpイベントにはこちらを登録する。
    public void OnJump(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!context.performed) return;
        JumpPressed = true;
        Debug.Log("Jump");
    }
    // PlayerInputのOnPickUPイベントにはこちらを登録する。
    public void OnPickUP(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!context.performed) return;

        if (owner != null && owner.cameraController != null)
            owner.cameraController.StandbyPickUpItem();
    }

    #region PlayerInputのスロット関連
    public void OnEquipPickelSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, PICKEL_SLOT);
    }
    public void OnEquipItemSlot1(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, ITEM_SLOT_1);
    }
    public void OnEquipItemSlot2(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, ITEM_SLOT_2);
    }
    public void OnEquipItemSlot3(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, ITEM_SLOT_3);
    }
    public void OnEquipItemSlot4(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, ITEM_SLOT_4);
    }
    public void OnEquipItemSlot5(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.Equip(this.owner, ITEM_SLOT_5);
    }
    public void OnNextSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;

        if (!context.performed) return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y < 0)
        {
            //Debug.Log("上にスクロール");
            owner.equipmentManager.NextItemEquip(this.owner);
        }
    }
    public void OnPreviousSlot(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (owner == null || owner.equipmentManager == null) return;

        if (!context.performed) return;

        Vector2 scroll = context.ReadValue<Vector2>();

        if (scroll.y > 0)
        {
            //Debug.Log("下にスクロール");
            owner.equipmentManager.PreviousItemEquip(this.owner);
        }
    }

    #endregion

    // BuildingのWallは、建築モードへの入口も兼ねる。
    public void OnBuildWall(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanSelectBuilding(context)) return;
        BuildWallPressed = true;
        Debug.Log("Build Wall");
    }

    // BuildingのRampは、建築モードへの入口も兼ねる。
    public void OnBuildRamp(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanSelectBuilding(context)) return;
        BuildRampPressed = true;
        Debug.Log("Build Ramp");
    }

    // BuildingのFloorは、建築モードへの入口も兼ねる。
    public void OnBuildFloor(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanSelectBuilding(context)) return;
        BuildFloorPressed = true;
        Debug.Log("Build Floor");
    }

    // BuildingのConeは、建築モードへの入口も兼ねる。
    public void OnBuildCone(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanSelectBuilding(context)) return;
        BuildConePressed = true;
        Debug.Log("Build Cone");
    }


    public void ResetJump() { JumpPressed = false; }
    public void ResetBuildWall() { BuildWallPressed = false; }
    public void ResetBuildRamp() { BuildRampPressed = false; }
    public void ResetBuildFloor() { BuildFloorPressed = false; }
    public void ResetBuildCone() { BuildConePressed = false; }

    // CombatのPrimary / Secondary / Reloadは、PlayerInputControllerがPlayerManagerに登録されている場合にのみ有効。
    public void OnUsePrimary(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanUseCombat(context)) return;
        owner.equipmentManager.GetcurrentItem()?.UsePrimary(context);
    }

    // PlayerInputのADSイベントにはこちらを登録する。
    public void OnUseSecondary(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanUseCombat(context)) return;
        if (context.started) isADS = true;
        else if (context.canceled) isADS = false;
        else return;
        owner.equipmentManager.GetcurrentItem()?.UseSecondary(isADS);
    }

    // Combat/Reloadのイベントにはこちらを登録する。
    public void OnCombatReload(InputAction.CallbackContext context)
    {
        if (!CanProcessInput) return;
        if (!CanUseCombat(context) || !context.performed) return;
        OnUseReload();
    }
    // PlayerInputのReloadイベントにはこちらを登録する。
    public void OnUseReload()
    {
        if (!CanProcessInput) return;
        if (!combatEnabled || owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.GetcurrentItem()?.UseReload();
    }
    // PlayerInputControllerをPlayerManagerに登録する。PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
    public void RegisterPlayer(PlayerManager _owner) { owner = _owner; }

    // PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
    private void OnDisable() { StopLocalInput(); }

    private void StopLocalInput()
    {
        localInputActive = false;
        if (playerInput != null) playerInput.enabled = false;
        combatEnabled = false;
        StopCombat();
        if (combatMap != null) combatMap.Disable();
        if (buildingMap != null) buildingMap.Disable();
        if (playerMap != null) playerMap.Disable();
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        ResetJump();
        ResetBuildWall();
        ResetBuildRamp();
        ResetBuildFloor();
        ResetBuildCone();
    }
}


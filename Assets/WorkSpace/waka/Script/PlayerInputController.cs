using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    public PlayerManager owner;

    [Header("入力と建築管理")]
    [Tooltip("同じPlayerに付いたUnity標準のPlayer Inputコンポーネント")]
    public UnityEngine.InputSystem.PlayerInput playerInput;
    public BuildingSystem buildingSystem;
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
    public bool BuildHeld => buildingMap != null && buildingMap.enabled &&
        buildAction != null && buildAction.enabled && buildAction.IsPressed();
    private bool combatEnabled;
    private bool started;

    private void Awake()
    {
        // PlayerInputが設定されていない場合は、同じGameObjectに付いたPlayerInputを探す。
        if (owner == null) owner = GetComponent<PlayerManager>();
        if (playerInput == null)
            playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
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
        // PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
        started = true;
        if (buildingSystem == null)
        {
            //BuildingSystemが設定されていない場合は、同じPlayerに付いたBuildingSystemを探す。
            foreach (BuildingSystem candidate in FindObjectsOfType<BuildingSystem>())
            {
                if (candidate.input == this)
                {
                    buildingSystem = candidate;
                    break;
                }
            }
        }
        if (!ResolveMaps())
        {
            Debug.LogError("PlayerInputController: Player Inputと、Player / Combat / BuildingのMapとBuilding内のPlace / Wall / Ramp / Floor / Coneを設定してください。", this);
            return;
        }
        if (buildingSystem == null)
            Debug.LogWarning("PlayerInputController: Building Systemを設定してください。", this);
        RestoreInputMode();
    }

    private void OnEnable()
    {
        // Startより前にOnEnableが呼ばれる場合があるので、ResolveMapsが成功していればRestoreInputModeする。
        if (started && ResolveMaps()) RestoreInputMode();
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
        if (!ResolveMaps() || !isActiveAndEnabled) return;
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
        if (buildingSystem == null || !buildingSystem.isActiveAndEnabled)
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
        MoveInput = context.ReadValue<Vector2>();
        Debug.Log("Move : " + MoveInput);
    }

    // PlayerInputのLookイベントにはこちらを登録する。
    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    // PlayerInputのJumpイベントにはこちらを登録する。
    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        JumpPressed = true;
        Debug.Log("Jump");
    }

    // BuildingのWallは、建築モードへの入口も兼ねる。
    public void OnBuildWall(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context)) return;
        BuildWallPressed = true;
        Debug.Log("Build Wall");
    }

    // BuildingのRampは、建築モードへの入口も兼ねる。
    public void OnBuildRamp(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context)) return;
        BuildRampPressed = true;
        Debug.Log("Build Ramp");
    }

    // BuildingのFloorは、建築モードへの入口も兼ねる。
    public void OnBuildFloor(InputAction.CallbackContext context)
    {
        if (!CanSelectBuilding(context)) return;
        BuildFloorPressed = true;
        Debug.Log("Build Floor");
    }

    // BuildingのConeは、建築モードへの入口も兼ねる。
    public void OnBuildCone(InputAction.CallbackContext context)
    {
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
        if (!CanUseCombat(context)) return;
        owner.equipmentManager.GetcurrentItem()?.UsePrimary(context);
    }

    // PlayerInputのADSイベントにはこちらを登録する。
    public void OnUseSecondary(InputAction.CallbackContext context)
    {
        if (!CanUseCombat(context)) return;
        if (context.started) isADS = true;
        else if (context.canceled) isADS = false;
        else return;
        owner.equipmentManager.GetcurrentItem()?.UseSecondary(isADS);
    }

    // Combat/Reloadのイベントにはこちらを登録する。
    public void OnCombatReload(InputAction.CallbackContext context)
    {
        if (!CanUseCombat(context) || !context.performed) return;
        OnUseReload();
    }
    // PlayerInputのReloadイベントにはこちらを登録する。
    public void OnUseReload()
    {
        if (!combatEnabled || owner == null || owner.equipmentManager == null) return;
        owner.equipmentManager.GetcurrentItem()?.UseReload();
    }
    // PlayerInputControllerをPlayerManagerに登録する。PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
    public void RegisterPlayer(PlayerManager _owner) { owner = _owner; }

    // PlayerInputが無効化されている場合は、ResolveMapsが失敗する。
    private void OnDisable()
    {
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
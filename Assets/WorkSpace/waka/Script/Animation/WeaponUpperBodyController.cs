using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class WeaponUpperBodyController : NetworkBehaviour
{
    [Header("キャラクターのAnimator")]
    [SerializeField] private Animator animator;

    [Header("上半身レイヤー名")]
    [SerializeField] private string layerName = "UpperBody";

    [Header("構えステート名")]
    [SerializeField] private string pistolState = "PistolAim";

    [SerializeField]
    private string assaultState = "HumanF@AssaultRifle_Aim01";

    [SerializeField] private string pickaxeState = "PickaxeHold";

    [Header("構えの切り替え時間")]
    [Min(0f)]
    [SerializeField] private float transitionTime = 0.15f;

    [Header("移動アニメーションの同期")]
    [Range(5f, 30f)]
    [SerializeField] private float sendRate = 15f;

    [Min(0f)]
    [SerializeField] private float remoteSmoothTime = 0.08f;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int MoveXHash =
        Animator.StringToHash("MoveX");

    private static readonly int MoveYHash =
        Animator.StringToHash("MoveY");

    // x = Speed、y = MoveX、z = MoveY
    private readonly SyncVar<Vector3> networkMovement =
        new SyncVar<Vector3>(Vector3.zero);

    private readonly SyncVar<WeaponAnimationType> networkWeaponType =
        new SyncVar<WeaponAnimationType>(WeaponAnimationType.None);

    private int layerIndex = -1;
    private bool initialized;
    private float targetWeight;

    private WeaponAnimationType requestedType =
        WeaponAnimationType.None;

    private WeaponAnimationType currentType =
        WeaponAnimationType.None;

    private Vector3 lastSentMovement;
    private WeaponAnimationType lastSentType;
    private bool hasSent;
    private float nextSendTime;
    private float lastSendTime;

    private void Awake()
    {
        InitializeAnimator();
    }

    private bool InitializeAnimator()
    {
        if (initialized)
            return true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                "AnimatorとAnimator Controllerを設定してください。",
                this);
            return false;
        }

        layerIndex = animator.GetLayerIndex(layerName);

        if (layerIndex < 0)
        {
            Debug.LogError(
                $"Animatorに「{layerName}」レイヤーがありません。",
                this);
            return false;
        }

        if (!HasFloatParameter(SpeedHash) ||
            !HasFloatParameter(MoveXHash) ||
            !HasFloatParameter(MoveYHash))
        {
            Debug.LogError(
                "AnimatorにFloat型のSpeed・MoveX・MoveYが必要です。",
                this);
            return false;
        }

        animator.SetLayerWeight(layerIndex, 0f);

        targetWeight = 0f;
        currentType = WeaponAnimationType.None;
        initialized = true;

        return true;
    }

    private bool HasFloatParameter(int hash)
    {
        foreach (AnimatorControllerParameter parameter
                 in animator.parameters)
        {
            if (parameter.nameHash == hash &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                return true;
            }
        }

        return false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!InitializeAnimator())
            return;

        hasSent = false;
        nextSendTime = 0f;

        if (IsOwner)
        {
            ApplyWeaponVisual(requestedType);
            SendAnimationState(true);
        }
        else
        {
            ApplyWeaponVisual(networkWeaponType.Value);
            ApplyMovementImmediately(networkMovement.Value);
        }
    }

    private void Update()
    {
        if (!initialized || animator == null)
            return;

        if (IsClientInitialized)
        {
            if (IsOwner)
            {
                SendAnimationState(false);
            }
            else
            {
                WeaponAnimationType receivedType =
                    networkWeaponType.Value;

                if (currentType != receivedType)
                    ApplyWeaponVisual(receivedType);

                ApplyRemoteMovement(networkMovement.Value);
            }
        }

        UpdateUpperBodyWeight();
    }

    private void UpdateUpperBodyWeight()
    {
        float currentWeight =
            animator.GetLayerWeight(layerIndex);

        float nextWeight = transitionTime <= 0f
            ? targetWeight
            : Mathf.MoveTowards(
                currentWeight,
                targetWeight,
                Time.deltaTime / transitionTime);

        animator.SetLayerWeight(layerIndex, nextWeight);
    }

    private Vector3 ReadMovement()
    {
        return new Vector3(
            animator.GetFloat(SpeedHash),
            animator.GetFloat(MoveXHash),
            animator.GetFloat(MoveYHash));
    }

    private void SendAnimationState(bool force)
    {
        if (!IsClientInitialized || !IsOwner || !initialized)
            return;

        float now = Time.unscaledTime;

        if (!force && now < nextSendTime)
            return;

        nextSendTime = now + 1f / Mathf.Max(1f, sendRate);

        Vector3 movement = ReadMovement();

        bool movementChanged =
            (movement - lastSentMovement).sqrMagnitude > 0.000001f;

        bool weaponChanged = requestedType != lastSentType;
        bool heartbeat = now - lastSendTime >= 0.5f;

        if (!force && hasSent &&
            !movementChanged && !weaponChanged && !heartbeat)
        {
            return;
        }

        SendAnimationServerRpc(movement, requestedType);

        lastSentMovement = movement;
        lastSentType = requestedType;
        lastSendTime = now;
        hasSent = true;
    }

    [ServerRpc]
    private void SendAnimationServerRpc(
        Vector3 movement,
        WeaponAnimationType weaponType)
    {
        if (!IsFinite(movement.x) ||
            !IsFinite(movement.y) ||
            !IsFinite(movement.z) ||
            !IsValidWeaponType(weaponType))
        {
            return;
        }

        movement.x = Mathf.Clamp01(movement.x);
        movement.y = Mathf.Clamp(movement.y, -1f, 1f);
        movement.z = Mathf.Clamp(movement.z, -1f, 1f);

        networkMovement.Value = movement;
        networkWeaponType.Value = weaponType;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsValidWeaponType(WeaponAnimationType type)
    {
        return type == WeaponAnimationType.None ||
               type == WeaponAnimationType.Pistol ||
               type == WeaponAnimationType.AssaultRifle ||
               type == WeaponAnimationType.Pickaxe;
    }

    private void ApplyMovementImmediately(Vector3 movement)
    {
        animator.SetFloat(SpeedHash, movement.x);
        animator.SetFloat(MoveXHash, movement.y);
        animator.SetFloat(MoveYHash, movement.z);
    }

    private void ApplyRemoteMovement(Vector3 movement)
    {
        if (remoteSmoothTime <= 0f)
        {
            ApplyMovementImmediately(movement);
            return;
        }

        animator.SetFloat(
            SpeedHash, movement.x,
            remoteSmoothTime, Time.deltaTime);

        animator.SetFloat(
            MoveXHash, movement.y,
            remoteSmoothTime, Time.deltaTime);

        animator.SetFloat(
            MoveYHash, movement.z,
            remoteSmoothTime, Time.deltaTime);
    }

    public void SetWeaponType(WeaponAnimationType type)
    {
        if (IsClientInitialized && !IsOwner)
            return;

        if (IsServerInitialized && !IsClientInitialized)
            return;

        if (!IsValidWeaponType(type))
            type = WeaponAnimationType.None;

        requestedType = type;

        if (!InitializeAnimator())
            return;

        ApplyWeaponVisual(type);
        SendAnimationState(true);
    }

    private void ApplyWeaponVisual(WeaponAnimationType type)
    {
        if (type == WeaponAnimationType.None)
        {
            currentType = WeaponAnimationType.None;
            targetWeight = 0f;

            if (transitionTime <= 0f)
                animator.SetLayerWeight(layerIndex, 0f);

            return;
        }

        string stateName;

        switch (type)
        {
            case WeaponAnimationType.Pistol:
                stateName = pistolState;
                break;

            case WeaponAnimationType.AssaultRifle:
                stateName = assaultState;
                break;

            case WeaponAnimationType.Pickaxe:
                stateName = pickaxeState;
                break;

            default:
                ApplyWeaponVisual(WeaponAnimationType.None);
                return;
        }

        // 同じ武器の構えを繰り返し先頭から再生しない。
        if (currentType == type)
            return;

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogError(
                $"{type}の構えステート名が未設定です。",
                this);

            currentType = type;
            targetWeight = 0f;
            return;
        }

        string fullPath = layerName + "." + stateName;
        int stateHash = Animator.StringToHash(fullPath);

        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogError(
                $"Animatorに「{fullPath}」がありません。",
                this);

            currentType = type;
            targetWeight = 0f;
            return;
        }

        if (animator.GetLayerWeight(layerIndex) <= 0.001f)
        {
            animator.Play(stateHash, layerIndex, 0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(
                stateHash,
                transitionTime,
                layerIndex,
                0f);
        }

        currentType = type;
        targetWeight = 1f;

        if (transitionTime <= 0f)
            animator.SetLayerWeight(layerIndex, 1f);
    }

    public void ClearWeapon()
    {
        SetWeaponType(WeaponAnimationType.None);
    }

    public void OnWeaponUnequipped()
    {
        ClearWeapon();
    }

    #region 追加：射撃・リロードの再生と同期

    [Header("アサルトの射撃・リロードステート名")]
    [SerializeField]
    private string assaultShootState =
        "HumanF@AssaultRifle_Aim01_Shoot01";

    [SerializeField]
    private string assaultReloadState =
        "HumanF@AssaultRifle_Reload01";

    [Header("ピストルの射撃・リロードステート名")]
    [SerializeField] private string pistolShootState;
    [SerializeField] private string pistolReloadState;

    [Header("射撃ステートで使用しているAnimation Clip")]
    [Tooltip("アサルトの射撃ステートのMotionと同じClipを登録")]
    [SerializeField] private AnimationClip assaultShootClip;

    [Tooltip("ピストルの射撃ステートのMotionと同じClipを登録")]
    [SerializeField] private AnimationClip pistolShootClip;

    [Header("射撃アニメーションの速度調整")]
    [Tooltip("Animatorに追加するFloatパラメーター名")]
    [SerializeField]
    private string shotSpeedParameter = "ShotAnimationSpeed";

    [Tooltip("次の弾までの時間の何割で再生するか。0.95なら95%。")]
    [Range(0.5f, 1f)]
    [SerializeField] private float shotTimeRatio = 0.95f;

    private bool warnedShotSettings;

    /// <summary>
    /// 以前の呼び出し方との互換用。
    /// 発射間隔が不明な場合は通常速度で再生する。
    /// </summary>
    public void NotifyWeaponShot(WeaponAnimationType type)
    {
        NotifyWeaponShot(type, 0f);
    }

    /// <summary>
    /// 発射成功時に、次の弾までの間隔を秒で渡す。
    /// </summary>
    public void NotifyWeaponShot(
        WeaponAnimationType type,
        float shotInterval)
    {
        SendWeaponAction(type, false, shotInterval);
    }

    public void NotifyWeaponReload(WeaponAnimationType type)
    {
        SendWeaponAction(type, true, 0f);
    }

    private void SendWeaponAction(
        WeaponAnimationType type,
        bool reload,
        float shotInterval)
    {
        if (!isActiveAndEnabled || !IsGunType(type))
            return;

        if (!IsFinite(shotInterval) || shotInterval < 0f)
            shotInterval = 0f;

        if (IsServerInitialized)
        {
            WeaponActionObserversRpc(type, reload, shotInterval);
        }
        else if (IsClientInitialized && IsOwner)
        {
            WeaponActionServerRpc(type, reload, shotInterval);
        }
    }

    private static bool IsGunType(WeaponAnimationType type)
    {
        return type == WeaponAnimationType.Pistol ||
               type == WeaponAnimationType.AssaultRifle;
    }

    // アニメーションの通知専用。
    // 弾数・ダメージ・実際の発射間隔は変更しない。
    [ServerRpc]
    private void WeaponActionServerRpc(
        WeaponAnimationType type,
        bool reload,
        float shotInterval)
    {
        if (!IsGunType(type) ||
            !IsFinite(shotInterval) ||
            shotInterval < 0f)
        {
            return;
        }

        WeaponActionObserversRpc(type, reload, shotInterval);
    }

    // 過去の射撃を途中参加者へ再生しない。
    [ObserversRpc]
    private void WeaponActionObserversRpc(
        WeaponAnimationType type,
        bool reload,
        float shotInterval)
    {
        PlayWeaponAction(type, reload, shotInterval);
    }

    private void PlayWeaponAction(
        WeaponAnimationType type,
        bool reload,
        float shotInterval)
    {
        if (!isActiveAndEnabled || !InitializeAnimator())
            return;

        WeaponAnimationType equippedType =
            IsOwner ? requestedType : networkWeaponType.Value;

        if (equippedType != type)
            return;

        string stateName;

        switch (type)
        {
            case WeaponAnimationType.AssaultRifle:
                stateName = reload
                    ? assaultReloadState
                    : assaultShootState;
                break;

            case WeaponAnimationType.Pistol:
                stateName = reload
                    ? pistolReloadState
                    : pistolShootState;
                break;

            default:
                return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning(
                $"{type}の{(reload ? "リロード" : "射撃")}" +
                "ステート名を設定してください。",
                this);
            return;
        }

        int stateHash =
            Animator.StringToHash(layerName + "." + stateName);

        if (!animator.HasState(layerIndex, stateHash))
        {
            Debug.LogWarning(
                $"Animatorに「{layerName}.{stateName}」がありません。",
                this);
            return;
        }

        ApplyWeaponVisual(type);

        if (!reload)
        {
            ApplyShotPlaybackSpeed(type, shotInterval);
        }

        // 射撃は発射間隔に合う速度で再生。
        // リロードの再生速度は変更しない。
        animator.Play(stateHash, layerIndex, 0f);
    }

    private void ApplyShotPlaybackSpeed(
        WeaponAnimationType type,
        float shotInterval)
    {
        int parameterHash =
            Animator.StringToHash(shotSpeedParameter);

        if (!HasFloatParameter(parameterHash))
        {
            WarnShotSettings(
                $"AnimatorにFloat型の「{shotSpeedParameter}」を追加し、" +
                "射撃ステートのSpeed Multiplierに設定してください。");
            return;
        }

        AnimationClip clip =
            type == WeaponAnimationType.AssaultRifle
                ? assaultShootClip
                : pistolShootClip;

        if (clip == null || clip.length <= 0f)
        {
            animator.SetFloat(parameterHash, 1f);

            WarnShotSettings(
                $"{type}のShoot Clipが未設定です。" +
                "射撃ステートのMotionと同じAnimation Clipを登録してください。");
            return;
        }

        // 間隔が渡されなかった場合は通常速度。
        if (shotInterval <= 0f)
        {
            animator.SetFloat(parameterHash, 1f);
            return;
        }

        float duration = Mathf.Max(
            0.01f,
            shotInterval * Mathf.Clamp(shotTimeRatio, 0.5f, 1f));

        // 例：0.5秒のClipを0.1秒で再生するなら速度5。
        float playbackSpeed = clip.length / duration;

        animator.SetFloat(parameterHash, playbackSpeed);
    }

    private void WarnShotSettings(string message)
    {
        if (warnedShotSettings)
            return;

        warnedShotSettings = true;
        Debug.LogWarning("[射撃アニメーション] " + message, this);
    }

    #endregion
}
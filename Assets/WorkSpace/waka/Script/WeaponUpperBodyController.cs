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
    [Tooltip("１秒間に送信する最大回数")]
    [Range(5f, 30f)]
    [SerializeField] private float sendRate = 15f;

    [Tooltip("他プレイヤーの移動アニメーションの補間時間")]
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

    // 自分が要求している装備種類
    private WeaponAnimationType requestedType =
        WeaponAnimationType.None;

    // この画面で現在表示している構え
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
        foreach (AnimatorControllerParameter parameter in animator.parameters)
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
            // ネットワーク開始前に装備された場合も反映する。
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
                // PlayerMovementが設定したAnimatorの値を送る。
                SendAnimationState(false);
            }
            else
            {
                // 他人のPlayerには受信した値だけを適用する。
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

        bool weaponChanged =
            requestedType != lastSentType;

        // 変化がなくても定期的に現在値を送る。
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

    // デフォルトの所有者制限を使用。
    // このPlayerの所有者だけが呼べる。
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
            SpeedHash,
            movement.x,
            remoteSmoothTime,
            Time.deltaTime);

        animator.SetFloat(
            MoveXHash,
            movement.y,
            remoteSmoothTime,
            Time.deltaTime);

        animator.SetFloat(
            MoveYHash,
            movement.z,
            remoteSmoothTime,
            Time.deltaTime);
    }

    /// <summary>
    /// 装備処理から呼ぶ。これまでと同じ呼び出し方で使用できる。
    /// </summary>
    public void SetWeaponType(WeaponAnimationType type)
    {
        // 通信開始後、他人のPlayerの装備表示を
        // ローカルの装備処理で上書きしない。
        if (IsClientInitialized && !IsOwner)
            return;

        // 専用サーバー上の装備処理からは送信しない。
        if (IsServerInitialized && !IsClientInitialized)
            return;

        if (!IsValidWeaponType(type))
            type = WeaponAnimationType.None;

        requestedType = type;

        if (!InitializeAnimator())
            return;

        // 自分の画面では通信を待たずに表示する。
        ApplyWeaponVisual(type);

        // 装備変更は通常の送信間隔を待たずに送る。
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

        // 同じ種類の受信でアニメーションを毎回先頭に戻さない。
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
                $"Animatorに「{fullPath}」がありません。" +
                "Inspectorのステート名を確認してください。",
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
}
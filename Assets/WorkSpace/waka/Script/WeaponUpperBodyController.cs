using UnityEngine;

public class WeaponUpperBodyController : MonoBehaviour
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

    [Header("切り替え時間")]
    [Min(0f)]
    [SerializeField] private float transitionTime = 0.15f;

    private int layerIndex = -1;
    private bool initialized;
    private float targetWeight;

    private WeaponAnimationType currentType =
        WeaponAnimationType.None;

    private void Awake()
    {
        Initialize();
    }

    private bool Initialize()
    {
        if (initialized)
            return true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

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

        animator.SetLayerWeight(layerIndex, 0f);
        targetWeight = 0f;
        initialized = true;

        return true;
    }

    private void Update()
    {
        if (!initialized || animator == null)
            return;

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

    /// <summary>
    /// 装備したアイテムの種類に応じて構えを変更
    /// </summary>
    public void SetWeaponType(WeaponAnimationType type)
    {
        if (!Initialize())
            return;

        if (type == WeaponAnimationType.None)
        {
            ClearWeapon();
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
                ClearWeapon();
                return;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogError(
                $"{type}の構えステート名が未設定です。",
                this);

            ClearWeapon();
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

            ClearWeapon();
            return;
        }

        if (currentType != type)
        {
            // 通常状態から装備した場合
            if (animator.GetLayerWeight(layerIndex) <= 0.001f)
            {
                animator.Play(stateHash, layerIndex, 0f);
            }
            else
            {
                // 別の武器へ持ち替えた場合
                animator.CrossFadeInFixedTime(
                    stateHash,
                    transitionTime,
                    layerIndex,
                    0f);
            }
        }

        currentType = type;
        targetWeight = 1f;

        if (transitionTime <= 0f)
            animator.SetLayerWeight(layerIndex, 1f);
    }

    /// <summary>
    /// 構えを解除して通常アニメーションへ戻す
    /// </summary>
    public void ClearWeapon()
    {
        currentType = WeaponAnimationType.None;
        targetWeight = 0f;

        if (initialized &&
            animator != null &&
            transitionTime <= 0f)
        {
            animator.SetLayerWeight(layerIndex, 0f);
        }
    }

    public void OnWeaponUnequipped()
    {
        ClearWeapon();
    }
}
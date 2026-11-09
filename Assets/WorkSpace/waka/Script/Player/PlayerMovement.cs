using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

public class PlayerMovement : NetworkBehaviour
{
    [Header("参照")]
    private PlayerManager myPlayer;
    public PlayerInputController input;
    public Transform cameraTransform;
    public Transform groundCheck;

    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("体の回転設定")]
    public float moveRotateSpeed = 15f;
    public float idleRotateStartAngle = 45f;
    public float idleRotateSpeed = 6f;

    [Header("ジャンプ調整")]
    public float fallMultiplier = 2.5f;

    [Header("接地判定")]
    public float groundDistance = 0.2f;
    public LayerMask groundLayer;

    [Header("アニメーション")]
    [SerializeField] private Animator animator;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int MoveXHash =
        Animator.StringToHash("MoveX");

    private static readonly int MoveYHash =
        Animator.StringToHash("MoveY");

    private Rigidbody rb;

    [Header("坂での停止")]
    [Range(0f, 89f)] public float maxGroundAngle = 55f;
    [Range(0f, 0.5f)] public float inputDeadZone = 0.1f;

    private bool isGrounded;
    private bool isJumping;
    private float ignoreGroundUntil;
    private Vector3 groundNormal = Vector3.up;
    private bool positionLocked;
    private RigidbodyConstraints constraintsBeforeLock;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner)
        {
            enabled = false;
        }

#if UNITY_EDITOR
        // Unity Editorでのみ
        GameDebug.Log($"{gameObject.name}:Editer");
#else
        // Editor以外（ビルド時）でのみ
        GameDebug.Log($"{gameObject.name}:ビルド");
#endif
    }

    private Vector2 GetMoveInput()
    {
        Vector2 value =
            Vector2.ClampMagnitude(input.MoveInput, 1f);

        return value.sqrMagnitude <= inputDeadZone * inputDeadZone
            ? Vector2.zero
            : value;
    }

    private void UpdateGrounded()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        if (Time.time < ignoreGroundUntil ||
            (isJumping && rb.velocity.y > 0f))
        {
            return;
        }

        const float lift = 0.2f;

        if (Physics.Raycast(
            groundCheck.position + Vector3.up * lift,
            Vector3.down,
            out RaycastHit hit,
            lift + groundDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody == rb)
                return;

            if (Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle)
            {
                isGrounded = true;
                isJumping = false;
                groundNormal = hit.normal;
            }
        }
    }

    private void LockPosition()
    {
        if (!positionLocked)
        {
            constraintsBeforeLock = rb.constraints;
            rb.constraints |= RigidbodyConstraints.FreezePosition;
            positionLocked = true;
        }

        rb.velocity = Vector3.zero;
    }

    private void UnlockPosition()
    {
        if (!positionLocked || rb == null)
            return;

        rb.constraints = constraintsBeforeLock;
        positionLocked = false;
    }

    private void OnDisable()
    {
        UnlockPosition();
    }

    /// <summary>
    /// 自身の登録
    /// </summary>
    public void RegisterPlayer(PlayerManager _myPlayer)
    {
        myPlayer = _myPlayer;
    }

    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(Transform _cameraTrans)
    {
        Debug.Log("PlayerMovement:Initialized!");
        cameraTransform = _cameraTrans;
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError(
                "PlayerMovementにRigidbodyが必要です。",
                this);

            enabled = false;
            return;
        }

        // 転倒防止
        rb.freezeRotation = true;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // 追加：しゃがみの初期化
        InitializeCrouch();
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        if (myPlayer == null || !myPlayer.IsInitialized ||
            rb == null || input == null ||
            cameraTransform == null || groundCheck == null)
        {
            UnlockPosition();
            return;
        }

        UpdateGrounded();

        // 追加：左Ctrlによるしゃがみ
        UpdateCrouch();

        // 追加：しゃがみ中はジャンプしない。
        bool jumpRequested =
            input.JumpPressed && !isCrouching;

        // 移動・ジャンプ・足場消失時は固定を解除する。
        bool shouldLock =
            isGrounded &&
            GetMoveInput() == Vector2.zero &&
            !jumpRequested;

        if (!shouldLock)
            UnlockPosition();

        Jump();
        Move();
        RotateBodyWhenIdle();
        BetterJump();

        // 静止した建築上で、物理演算による微小な滑りも防ぐ。
        if (shouldLock && isGrounded)
            LockPosition();
    }

    private void Move()
    {
        Vector2 moveInput = GetMoveInput();

        // 追加：しゃがみ中は移動速度を下げる。
        float currentMoveSpeed =
            isCrouching ? crouchMoveSpeed : moveSpeed;

        // カメラのY回転だけ使う
        Quaternion cameraYawRotation =
            Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

        Vector3 forward = cameraYawRotation * Vector3.forward;
        Vector3 right = cameraYawRotation * Vector3.right;

        // カメラ基準移動
        Vector3 moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        if (isGrounded)
        {
            // 地面にいる間は重力を打ち消し、坂の面に沿って移動する。
            if (rb.useGravity)
                rb.AddForce(-Physics.gravity, ForceMode.Acceleration);

            Vector3 slopeDirection =
                Vector3.ProjectOnPlane(moveDirection, groundNormal);

            rb.velocity =
                slopeDirection.normalized *
                moveInput.magnitude *
                currentMoveSpeed;
        }
        else
        {
            Vector3 velocity = rb.velocity;
            velocity.x = moveDirection.x * currentMoveSpeed;
            velocity.z = moveDirection.z * currentMoveSpeed;
            rb.velocity = velocity;
        }

        // 2D Blend Tree用にアニメーションを更新
        if (animator != null)
        {
            // 斜め入力も長さ1以内にする
            Vector2 animationInput =
                Vector2.ClampMagnitude(moveInput, 1f);

            animator.SetFloat(
                SpeedHash,
                animationInput.magnitude,
                0.1f,
                Time.fixedDeltaTime);

            // 左：-1、右：1、停止：0
            animator.SetFloat(
                MoveXHash,
                animationInput.x,
                0.1f,
                Time.fixedDeltaTime);

            // 後退：-1、前進：1、停止：0
            animator.SetFloat(
                MoveYHash,
                animationInput.y,
                0.1f,
                Time.fixedDeltaTime);
        }

        // 移動中は体をカメラ正面へ向ける
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(forward, Vector3.up);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    moveRotateSpeed * Time.fixedDeltaTime));
        }
    }

    private void RotateBodyWhenIdle()
    {
        Vector2 moveInput = GetMoveInput();

        // 入力中はMove側で回転する
        if (moveInput.sqrMagnitude > 0.01f)
            return;

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.01f)
            return;

        cameraForward.Normalize();

        Vector3 bodyForward = transform.forward;
        bodyForward.y = 0f;
        bodyForward.Normalize();

        float angle = Vector3.SignedAngle(
            bodyForward,
            cameraForward,
            Vector3.up);

        float absAngle = Mathf.Abs(angle);

        // 45度以内なら体は回さない
        if (absAngle < idleRotateStartAngle)
            return;

        // ADS中なら回さない
        if (myPlayer.inputController.isADS)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(cameraForward, Vector3.up);

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                idleRotateSpeed * Time.fixedDeltaTime));
    }

    private void Jump()
    {
        if (input.JumpPressed && isGrounded && !isCrouching)
        {
            UnlockPosition();

            isGrounded = false;
            isJumping = true;
            ignoreGroundUntil = Time.time + 0.15f;

            Vector3 velocity = rb.velocity;
            velocity.y = 0f;
            rb.velocity = velocity;

            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse);
        }

        input.ResetJump();
    }

    private void BetterJump()
    {
        // 接地中の下り坂には落下加速を加えない。
        if (!isGrounded && rb.velocity.y < 0f)
        {
            rb.AddForce(
                Vector3.up * Physics.gravity.y * (fallMultiplier - 1f),
                ForceMode.Acceleration);
        }
    }

    #region 追加：左Ctrlでしゃがむ

    [Header("しゃがみ")]
    [SerializeField] private CapsuleCollider playerCapsule;

    [Min(0f)]
    [SerializeField] private float crouchMoveSpeed = 2f;

    [Tooltip("通常の高さに対する、しゃがみ中の高さの割合")]
    [Range(0.3f, 1f)]
    [SerializeField] private float crouchHeightRatio = 0.6f;

    [Tooltip("立ち上がりを妨げる地面・建築・障害物のLayer")]
    [SerializeField] private LayerMask standObstacleLayers = ~0;

    private static readonly int CrouchHash =
        Animator.StringToHash("IsCrouching");

    private float standingHeight;
    private Vector3 standingCenter;
    private bool crouchReady;
    private bool hasCrouchParameter;
    private bool isCrouching;

    public bool IsCrouching => isCrouching;

    private void InitializeCrouch()
    {
        if (playerCapsule == null)
            playerCapsule = GetComponent<CapsuleCollider>();

        if (playerCapsule == null || playerCapsule.direction != 1)
        {
            Debug.LogWarning(
                "しゃがみにはY-Axis方向のCapsuleColliderが必要です。",
                this);
            return;
        }

        standingHeight = playerCapsule.height;
        standingCenter = playerCapsule.center;
        crouchReady = true;

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == CrouchHash &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasCrouchParameter = true;
                    break;
                }
            }

            if (!hasCrouchParameter)
            {
                Debug.LogWarning(
                    "AnimatorにBool型のIsCrouchingを追加してください。" +
                    "追加するまでは、当たり判定と移動速度だけ変わります。",
                    this);
            }
        }
    }

    private void UpdateCrouch()
    {
        if (!crouchReady)
            return;

        // Input Systemで左Ctrlを取得。
        bool crouchHeld =
            Application.isFocused &&
            Keyboard.current != null &&
            Keyboard.current.leftCtrlKey.isPressed;

        if (crouchHeld)
        {
            // 地面にいるときだけ、しゃがみ始める。
            if (isGrounded && !isCrouching)
            {
                SetCrouching(true);
            }
        }
        else if (isCrouching)
        {
            // 頭上に障害物がある間は立ち上がらない。
            if (CanStandUp())
            {
                SetCrouching(false);
            }
        }

        if (animator != null && hasCrouchParameter)
        {
            animator.SetBool(CrouchHash, isCrouching);
        }
    }

    private void SetCrouching(bool crouching)
    {
        isCrouching = crouching;

        if (crouching)
        {
            float height = Mathf.Clamp(
                standingHeight * crouchHeightRatio,
                playerCapsule.radius * 2f,
                standingHeight);

            // 足元の高さを保ったまま、中心を下げる。
            Vector3 center = standingCenter;
            center.y -= (standingHeight - height) * 0.5f;

            playerCapsule.height = height;
            playerCapsule.center = center;
        }
        else
        {
            playerCapsule.height = standingHeight;
            playerCapsule.center = standingCenter;
        }
    }

    private bool CanStandUp()
    {
        Transform capsuleTransform = playerCapsule.transform;
        Vector3 scale = capsuleTransform.lossyScale;

        float radius =
            playerCapsule.radius *
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

        float height = Mathf.Max(
            standingHeight * Mathf.Abs(scale.y),
            radius * 2f);

        Vector3 center =
            capsuleTransform.TransformPoint(standingCenter);

        Vector3 up = capsuleTransform.up;

        float halfSegment =
            Mathf.Max(0f, height * 0.5f - radius);

        Vector3 bottom = center - up * halfSegment;
        Vector3 top = center + up * halfSegment;

        // 地面に接しているだけで立てなくなることを避けるため、
        // 検査用の半径を少しだけ小さくする。
        float margin = Mathf.Min(0.02f, radius * 0.1f);
        float checkRadius = Mathf.Max(0.001f, radius - margin);

        Collider[] hits = Physics.OverlapCapsule(
            bottom,
            top,
            checkRadius,
            standObstacleLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            // 自分自身や子オブジェクトは除外する。
            if (hit == playerCapsule ||
                hit.attachedRigidbody == rb ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            // 立ったときの範囲に障害物がある。
            return false;
        }

        return true;
    }

    #endregion
}
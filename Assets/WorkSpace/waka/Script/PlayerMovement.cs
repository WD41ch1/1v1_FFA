using UnityEngine;
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

    private Vector2 GetMoveInput()
    {
        Vector2 value = Vector2.ClampMagnitude(input.MoveInput, 1f);
        return value.sqrMagnitude <= inputDeadZone * inputDeadZone
            ? Vector2.zero : value;
    }

    private void UpdateGrounded()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        if (Time.time < ignoreGroundUntil ||
            (isJumping && rb.velocity.y > 0f))
            return;

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

        // 転倒防止
        rb.freezeRotation = true;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner || myPlayer == null || !myPlayer.IsInitialized ||
            rb == null || input == null ||
            cameraTransform == null || groundCheck == null)
        {
            UnlockPosition();
            return;
        }

        UpdateGrounded();

        // 移動・ジャンプ・足場消失時は固定を解除する。
        bool shouldLock = isGrounded &&
            GetMoveInput() == Vector2.zero && !input.JumpPressed;

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

            rb.velocity = slopeDirection.normalized *
                moveInput.magnitude * moveSpeed;
        }
        else
        {
            Vector3 velocity = rb.velocity;
            velocity.x = moveDirection.x * moveSpeed;
            velocity.z = moveDirection.z * moveSpeed;
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
                Time.fixedDeltaTime
            );

            // 左：-1、右：1、停止：0
            animator.SetFloat(
                MoveXHash,
                animationInput.x,
                0.1f,
                Time.fixedDeltaTime
            );

            // 後退：-1、前進：1、停止：0
            animator.SetFloat(
                MoveYHash,
                animationInput.y,
                0.1f,
                Time.fixedDeltaTime
            );
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
                    moveRotateSpeed * Time.fixedDeltaTime
                )
            );
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

        float angle =
            Vector3.SignedAngle(
                bodyForward,
                cameraForward,
                Vector3.up
            );

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
                idleRotateSpeed * Time.fixedDeltaTime
            )
        );
    }

    private void Jump()
    {
        if (input.JumpPressed && isGrounded)
        {
            UnlockPosition();
            isGrounded = false;
            isJumping = true;
            ignoreGroundUntil = Time.time + 0.15f;

            Vector3 velocity = rb.velocity;
            velocity.y = 0f;
            rb.velocity = velocity;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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
}

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
        // 所有者以外、または初期化前は処理しない
        if (!IsOwner || myPlayer == null || !myPlayer.IsInitialized)
            return;

        if (rb == null ||
            input == null ||
            cameraTransform == null ||
            groundCheck == null)
            return;

        Move();
        RotateBodyWhenIdle();
        Jump();
        BetterJump();
    }

    private void Move()
    {
        Vector2 moveInput = input.MoveInput;

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

        Vector3 velocity = rb.velocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        rb.velocity = velocity;

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
        Vector2 moveInput = input.MoveInput;

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
        bool isGrounded =
            Physics.CheckSphere(
                groundCheck.position,
                groundDistance,
                groundLayer
            );

        if (input.JumpPressed && isGrounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );
        }

        input.ResetJump();
    }

    private void BetterJump()
    {
        if (rb.velocity.y < 0)
        {
            rb.AddForce(
                Vector3.up *
                Physics.gravity.y *
                (fallMultiplier - 1f),
                ForceMode.Acceleration
            );
        }
    }
}
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

public class PlayerMovement : MonoBehaviour
{
    [Header("参照")]
    private PlayerManager owner;
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

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 転倒防止
        rb.freezeRotation = true;
    }

    private void FixedUpdate()
    {
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

        // WASD入力中はMove側で回転するのでここでは何もしない
        if (moveInput.sqrMagnitude > 0.01f)
            return;

        // カメラの前方向を取得
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.01f)
            return;

        cameraForward.Normalize();

        // 体の前方向を取得
        Vector3 bodyForward = transform.forward;
        bodyForward.y = 0f;
        bodyForward.Normalize();

        // 体の正面とカメラ正面の角度差
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

        //  ADS中なら回さない
        if (owner.inputController.isADS)
            return;

            // 45度以上ズレたらカメラ方向へ回す
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

    /// <summary>
    /// PlayerManagerの登録
    /// </summary>
    /// <param name="_owner"></param>
    public void RegisterPlayer(PlayerManager _owner)
    {
        owner = _owner;
    }

}
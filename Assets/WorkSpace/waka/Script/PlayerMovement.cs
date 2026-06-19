using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("参照")]
    public PlayerInputController input;
    public Transform cameraTransform;
    public Transform groundCheck;

    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("体の回転設定")]
    public float rotateStartAngle = 60f;
    public float rotateFullAngle = 90f;
    public float minRotateSpeed = 2f;
    public float maxRotateSpeed = 10f;
    public float moveRotateSpeed = 15f;

    [Header("ジャンプ調整")]
    public float fallMultiplier = 2.5f;

    [Header("接地判定")]
    public float groundDistance = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private float lastMoveTime;
    private float lastLookTime;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Move();
        RotateBodyByCamera();
        Jump();
        BetterJump();


    }

    private void Update()
    {
        if (input.LookInput.sqrMagnitude > 0.01f)
        {
            lastLookTime = Time.time;
        }
    }

    private void Move()
    {
        Vector2 moveInput = input.MoveInput;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastMoveTime = Time.time;
        }

        // カメラのY回転だけ使う
        Quaternion cameraYawRotation =
            Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

        // カメラ基準の前後左右
        Vector3 forward = cameraYawRotation * Vector3.forward;
        Vector3 right = cameraYawRotation * Vector3.right;

        // WASDの移動方向
        Vector3 moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // 移動
        Vector3 velocity = rb.velocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        rb.velocity = velocity;

        // WASDどれかが押されている間は
        // 体をカメラ正面へ向ける
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(forward, Vector3.up);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    moveRotateSpeed * Time.fixedDeltaTime
                );
        }
    }

    private void RotateBodyByCamera()
    {
        Vector2 moveInput = input.MoveInput;

        // WASD入力中はMove()側で体をカメラ正面に向ける
        if (moveInput.sqrMagnitude > 0.01f)
            return;

        // 移動をやめた直後は回さない
        if (Time.time - lastMoveTime < 0.3f)
            return;

        // カメラを動かしていない時は回さない
        if (Time.time - lastLookTime > 0.1f)
            return;

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;

        if (cameraForward == Vector3.zero)
            return;

        cameraForward.Normalize();

        float cameraYaw =
            Quaternion.LookRotation(cameraForward).eulerAngles.y;

        float bodyYaw =
            transform.eulerAngles.y;

        float angle =
            Mathf.DeltaAngle(bodyYaw, cameraYaw);

        float absAngle =
            Mathf.Abs(angle);

        if (absAngle <= rotateStartAngle)
            return;

        float t =
            Mathf.InverseLerp(
                rotateStartAngle,
                rotateFullAngle,
                absAngle
            );

        float rotateSpeed =
            Mathf.Lerp(
                minRotateSpeed,
                maxRotateSpeed,
                t
            );

        Quaternion targetRotation =
            Quaternion.Euler(0f, cameraYaw, 0f);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.fixedDeltaTime
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
                (fallMultiplier - 1),
                ForceMode.Acceleration
            );
        }
    }
}
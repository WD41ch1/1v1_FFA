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

    [Header("ジャンプ調整")]
    public float fallMultiplier = 2.5f;

    [Header("接地判定")]
    public float groundDistance = 0.2f;
    public LayerMask groundLayer;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
        BetterJump();
    }

    private void Move()
    {
        Vector2 moveInput = input.MoveInput;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        Vector3 velocity = rb.velocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        rb.velocity = velocity;

        // 体は移動方向ではなく、カメラの正面方向を向く
        if (forward != Vector3.zero)
        {
            transform.forward = forward;
        }
    }

    private void Jump()
    {
        bool isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundDistance,
            groundLayer
        );

        if (input.JumpPressed && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        input.ResetJump();
    }
    private void BetterJump()
    {
        // 落下中だけ重力を強くする
        if (rb.velocity.y < 0)
        {
            rb.AddForce(
                Vector3.up * Physics.gravity.y * (fallMultiplier - 1),
                ForceMode.Acceleration
            );
        }
    }
}
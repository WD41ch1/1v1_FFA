using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("参照")]
    public PlayerInputController input;

    // カメラ
    public Transform cameraTransform;

    // 足元判定用オブジェクト
    public Transform groundCheck;

    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("体の回転設定")]
    // 何度差が付いたら体を回し始めるか
    public float rotateStartAngle = 60f;

    // この角度になったら最大速度で回る
    public float rotateFullAngle = 90f;

    // 最低回転速度
    public float minRotateSpeed = 2f;

    // 最大回転速度
    public float maxRotateSpeed = 10f;

    [Header("ジャンプ調整")]
    // 落下を速くする倍率
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
        // 移動
        Move();

        // カメラ方向に応じて体を回す
        RotateBodyByCamera();

        // ジャンプ
        Jump();

        // 落下補正
        BetterJump();
    }

    /// <summary>
    /// WASD移動
    /// カメラ基準で移動する
    /// フォートナイト風
    /// </summary>
    private void Move()
    {
        Vector2 moveInput = input.MoveInput;

        // カメラのY回転だけを使う
        // 上下の向きは無視して、横方向だけ見る
        Quaternion cameraYawRotation =
            Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

        // カメラ基準の前後左右
        Vector3 forward = cameraYawRotation * Vector3.forward;
        Vector3 right = cameraYawRotation * Vector3.right;

        // WASD入力をカメラ基準の移動方向に変換
        Vector3 moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        // 斜め移動が速くなりすぎないようにする
        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        Vector3 velocity = rb.velocity;

        // XZ方向だけ移動させる
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;

        rb.velocity = velocity;
    }

    /// <summary>
    /// カメラと体の角度差で体を回転
    ///
    /// 0～60度
    ///     回らない
    ///
    /// 60～90度
    ///     徐々に回る
    ///
    /// 90度以上
    ///     素早く回る
    /// </summary>
    private void RotateBodyByCamera()
    {
        // カメラの前方向取得
        Vector3 cameraForward = cameraTransform.forward;

        // 上下成分を無視
        cameraForward.y = 0f;

        if (cameraForward == Vector3.zero)
            return;

        cameraForward.Normalize();

        // カメラのY回転
        float cameraYaw =
            Quaternion.LookRotation(cameraForward).eulerAngles.y;

        // プレイヤーのY回転
        float bodyYaw =
            transform.eulerAngles.y;

        // カメラと体の角度差
        float angle =
            Mathf.DeltaAngle(bodyYaw, cameraYaw);

        float absAngle =
            Mathf.Abs(angle);

        // 60度以内なら回転しない
        if (absAngle <= rotateStartAngle)
            return;

        // 60～90度を0～1に変換
        float t =
            Mathf.InverseLerp(
                rotateStartAngle,
                rotateFullAngle,
                absAngle
            );

        // 回転速度補間
        float rotateSpeed =
            Mathf.Lerp(
                minRotateSpeed,
                maxRotateSpeed,
                t
            );

        // 目標回転
        Quaternion targetRotation =
            Quaternion.Euler(
                0f,
                cameraYaw,
                0f
            );

        // 滑らかに回転
        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.fixedDeltaTime
            );
    }

    /// <summary>
    /// ジャンプ
    /// </summary>
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

    /// <summary>
    /// 落下を速くして
    /// ジャンプを気持ちよくする
    /// </summary>
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
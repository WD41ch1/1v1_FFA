using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追従対象")]
    public Transform target;

    [Header("入力")]
    public PlayerInputController input;

    [Header("カメラ位置")]
    public Vector3 offset = new Vector3(0, 3, -6);

    [Header("感度")]
    public float mouseSensitivity = 2f;

    [Header("カメラの見る位置")]
    public float targetHeight = 1.5f;

    [Header("カメラ衝突")]
    public LayerMask collisionLayer;
    public float cameraRadius = 0.3f;
    public float wallOffset = 0.2f;

    [Header("近すぎ防止")]
    public float minDistance = 1.2f;
    public float closeCameraHeight = 2.2f;
    public float closeCameraBack = 0.4f;

    // 現在の左右回転角度
    public float Yaw => yaw;

    // 左右回転
    private float yaw;

    // 上下回転
    private float pitch = 15f;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null || input == null) return;

        Vector2 lookInput = input.LookInput;

        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // プレイヤーの胸〜顔あたりを見る
        Vector3 targetPoint =
            target.position + Vector3.up * targetHeight;

        // 本来置きたいカメラ位置
        Vector3 desiredPosition =
            targetPoint + rotation * offset;

        Vector3 direction =
            desiredPosition - targetPoint;

        float distance = direction.magnitude;

        direction.Normalize();

        Vector3 finalPosition = desiredPosition;

        // 壁に当たったら手前に寄せる
        if (Physics.SphereCast(
            targetPoint,
            cameraRadius,
            direction,
            out RaycastHit hit,
            distance,
            collisionLayer))
        {
            finalPosition =
                hit.point - direction * wallOffset;
        }

        // カメラがプレイヤーに近すぎる場合
        float currentDistance =
            Vector3.Distance(targetPoint, finalPosition);

        if (currentDistance < minDistance)
        {
            // カメラをプレイヤーの少し上＋少し後ろに逃がす
            Vector3 backDirection = -target.forward;

            finalPosition =
                target.position
                + Vector3.up * closeCameraHeight
                + backDirection * closeCameraBack;
        }

        transform.position = finalPosition;

        // プレイヤーの胸〜顔を見る
        transform.rotation = rotation;
    }
}
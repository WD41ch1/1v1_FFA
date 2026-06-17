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

    private float yaw;
    private float pitch = 15f;

    private void LateUpdate()
    {
        if (target == null || input == null) return;

        Vector2 lookInput = input.LookInput;

        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;

        // ★変更：上も下もかなり向けるようにした
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        transform.position = target.position + rotation * offset;

        // ★変更：LookAtではなく、カメラの回転をそのまま使う
        transform.rotation = rotation;
    }
}
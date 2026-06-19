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

    // 現在の左右回転角度
    public float Yaw => yaw;

    // 左右回転
    private float yaw;

    // 上下回転
    private float pitch = 15f;

    private void LateUpdate()
    {
        // 参照が設定されていなければ処理しない
        if (target == null || input == null) return;

        // マウス入力取得
        Vector2 lookInput = input.LookInput;

        // 左右回転
        yaw += lookInput.x * mouseSensitivity;

        // 上下回転
        pitch -= lookInput.y * mouseSensitivity;

        // 上下の角度制限
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        // カメラ回転を作成
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // プレイヤー中心からoffset分離れた位置へ移動
        transform.position = target.position + rotation * offset;

        // カメラの向きを設定
        transform.rotation = rotation;
    }
}
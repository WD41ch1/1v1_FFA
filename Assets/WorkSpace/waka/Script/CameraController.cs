using UnityEngine;
using static GameConst;

public class CameraController : MonoBehaviour
{
    [SerializeField]
    private Camera playerCamera;
    public Camera PlayerCamera => playerCamera;

    public PlayerManager owner;

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

        if (playerCamera != null)
            playerCamera.fieldOfView = DEFAULT_PLAYER_FOV;
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

    public void StandbyPickUpItem()
    {
        Ray ray = GetCrosshairRay();

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Rayが当たったオブジェクトからItemPickupを探す
            IPickupable item = hit.collider.GetComponentInParent<IPickupable>();

            if (item == null)
                return;

            // Playerとアイテムの距離を確認
            float distance = Vector3.Distance(
                transform.position,
                item.GetTransform().position
            );

            if (distance > PICKUP_DISTANCE)
                return;

            // 取得処理
            item.Pickup(owner);
            Destroy(item.GetTransform().gameObject);
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

    /// <summary>
    /// 画面中央(クロスヘア)からRayを飛ばす
    /// </summary>
    /// <returns></returns>
    public Ray GetCrosshairRay()
    {
        return playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    /// <summary>
    /// クロスヘアで何を狙ってるかを取得
    /// </summary>
    /// <param name="hit"></param>
    /// <returns></returns>
    public bool GetCrosshairTarget(out RaycastHit hit)
    {
        //  画面の中央からレイを飛ばす
        Ray ray = GetCrosshairRay();

        return Physics.Raycast(ray, out hit);
    }

}
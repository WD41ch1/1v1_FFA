using UnityEngine;
using static GameConst;

public class CameraController : MonoBehaviour
{
    // GUIManagerから開閉状態を設定する
    public static bool InventoryOpen { get; set; }

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

    public float Yaw => yaw;

    private float yaw;
    private float pitch = 15f;

    // 再生開始時に状態をリセット
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInventoryState()
    {
        InventoryOpen = false;
    }

    /// <summary>
    /// 初期化
    /// </summary>
    public void Initialize(
        PlayerManager _owner,
        Transform _target)
    {
        owner = _owner;
        input = owner != null ? owner.inputController : null;
        target = _target;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = DEFAULT_PLAYER_FOV;
        }
    }

    private void LateUpdate()
    {
        if (target == null || input == null)
            return;

        // インベントリを閉じているときだけ視点を回転する
        if (!InventoryOpen)
        {
            Vector2 lookInput = input.LookInput;

            yaw += lookInput.x * mouseSensitivity;
            pitch -= lookInput.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        // プレイヤーの胸〜顔あたりを基準にする
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
            Vector3 backDirection = -target.forward;

            finalPosition =
                target.position
                + Vector3.up * closeCameraHeight
                + backDirection * closeCameraBack;
        }

        transform.position = finalPosition;
        transform.rotation = rotation;
    }

    public void StandbyPickUpItem()
    {
        if (playerCamera == null || owner == null)
            return;

        Ray ray = GetCrosshairRay();

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            IPickupable item =
                hit.collider.GetComponentInParent<IPickupable>();

            if (item == null)
                return;

            float distance = Vector3.Distance(
                transform.position,
                item.GetTransform().position
            );

            if (distance > PICKUP_DISTANCE)
                return;

            // 既存の取得リクエスト処理
            item.RequestPickup(owner);
        }
    }

    /// <summary>
    /// 画面中央からRayを飛ばす
    /// </summary>
    public Ray GetCrosshairRay()
    {
        return playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));
    }

    /// <summary>
    /// クロスヘアで何を狙っているか取得する
    /// </summary>
    public bool GetCrosshairTarget(out RaycastHit hit)
    {
        if (playerCamera == null)
        {
            hit = default;
            return false;
        }

        Ray ray = GetCrosshairRay();

        return Physics.Raycast(ray, out hit);
    }
}
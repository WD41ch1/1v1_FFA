using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class CameraController : MonoBehaviour
{
    // それぞれのUIから開閉状態を知らせる
    public static bool InventoryOpen { get; set; }
    public static bool DisConnectMenuOpen { get; set; }

    public static bool IsCameraLocked =>
        InventoryOpen || DisConnectMenuOpen;

    [Header("カメラ")]
    [SerializeField] private Camera playerCamera;

    public Camera PlayerCamera => playerCamera;

    public PlayerManager owner;

    [Header("追従対象")]
    public Transform target;

    [Header("入力")]
    public PlayerInputController input;

    [Header("右肩越しのカメラ位置")]
    [Tooltip("X：右、Y：追加の高さ、Z：後ろへの距離")]
    public Vector3 offset = new Vector3(0.65f, 0.15f, -3.2f);

    [Header("感度")]
    public float mouseSensitivity = 0.5f;

    [Header("カメラの回転中心")]
    public float targetHeight = 1.5f;

    [Header("上下の視点")]
    [SerializeField] private float initialPitch = 10f;
    [SerializeField] private float minPitch = -65f;
    [SerializeField] private float maxPitch = 75f;

    [Header("カメラ衝突")]
    [Tooltip("地面・壁・床・階段・屋根のLayerを指定")]
    public LayerMask collisionLayer;

    [Min(0.01f)]
    public float cameraRadius = 0.18f;

    [Min(0f)]
    public float wallOffset = 0.04f;

    [Header("近すぎるときのモデル非表示")]
    public float minDistance = 0.65f;

    [SerializeField] private bool hidePlayerWhenClose = true;

    [Tooltip("自分の体・髪・装飾のRendererを登録。UIは登録しない")]
    [SerializeField] private Renderer[] playerRenderers;

    [Min(0f)]
    [SerializeField] private float showDistanceMargin = 0.15f;

    // 以前のフィールドとの互換用。
    // 壁の確認なしでカメラを移動する処理には使用しない。
    [HideInInspector] public float closeCameraHeight = 2.2f;
    [HideInInspector] public float closeCameraBack = 0.4f;

    [Header("壁から離れたときの戻り")]
    [Min(0f)]
    [SerializeField] private float distanceReturnTime = 0.15f;

    [Min(0f)]
    [SerializeField] private float shoulderReturnTime = 0.15f;

    [Header("回転中心が壁に重なった場合")]
    [Min(0.05f)]
    [SerializeField] private float maxPivotCorrection = 0.6f;

    [Header("近距離の描画")]
    [Min(0.01f)]
    [SerializeField] private float nearClipPlane = 0.05f;

    public float Yaw => yaw;

    private float yaw;
    private float pitch = 10f;

    private float currentDistance;
    private float distanceVelocity;

    private float shoulderAmount = 1f;
    private float shoulderVelocity;

    private bool positionInitialized;
    private bool hasSafePosition;
    private Vector3 lastSafePosition;

    private Transform playerRoot;

    private bool cursorReleased;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private SphereCollider collisionProbe;

    private bool modelHidden;

    private readonly Dictionary<Renderer, bool> savedRendererStates =
        new Dictionary<Renderer, bool>();

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMenuState()
    {
        InventoryOpen = false;
        DisConnectMenuOpen = false;
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        pitch = initialPitch;
    }

    public void Initialize(
        PlayerManager _owner,
        Transform _target)
    {
        RestorePlayerRenderers();

        owner = _owner;
        input = owner != null ? owner.inputController : null;
        target = _target;

        playerRoot = owner != null ? owner.transform : target;

        if (target != null)
            yaw = target.eulerAngles.y;

        pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);

        ResetCameraPosition();
    }

    private void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;

        playerRoot = owner != null ? owner.transform : target;

        if (playerCamera != null)
        {
            // 初期化時だけ設定し、武器側のADSを上書きしない。
            playerCamera.fieldOfView = DEFAULT_PLAYER_FOV;
            playerCamera.nearClipPlane =
                Mathf.Max(0.01f, nearClipPlane);
        }
    }

    private void ResetCameraPosition()
    {
        positionInitialized = false;
        hasSafePosition = false;

        distanceVelocity = 0f;
        shoulderVelocity = 0f;
        shoulderAmount = 1f;
    }

    private void LateUpdate()
    {
        // 自分が操作するカメラだけ処理する
        if (input == null || !input.CanProcessInput)
        {
            RestoreCursor();
            RestorePlayerRenderers();
            positionInitialized = false;
            return;
        }

        UpdateCursor();

        if (target == null || playerCamera == null)
        {
            RestorePlayerRenderers();
            return;
        }

        // UI中は視点回転のみ停止。
        // 追従と壁への対応は継続する。
        if (!IsCameraLocked && Application.isFocused)
        {
            Vector2 lookInput = input.LookInput;

            if (IsFinite(lookInput.x) && IsFinite(lookInput.y))
            {
                yaw += lookInput.x * mouseSensitivity;
                pitch -= lookInput.y * mouseSensitivity;

                yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
        }

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 originalPivot =
            target.position + Vector3.up * targetHeight;

        float radius = GetCollisionRadius();

        Vector3 pivot = originalPivot;

        // 判定開始位置が建築に埋まっている場合の補正。
        if (!TryResolvePivot(ref pivot, radius))
        {
            ApplyFallbackPosition(originalPivot, rotation, radius);
            return;
        }

        // 肩方向に壁がある場合は中央に寄せる。
        Vector3 fullShoulder =
            rotation * new Vector3(offset.x, offset.y, 0f);

        float shoulderLength = fullShoulder.magnitude;
        float allowedShoulder = 1f;

        if (shoulderLength > 0.001f)
        {
            float clearLength =
                GetClearDistance(pivot, fullShoulder, radius);

            allowedShoulder =
                Mathf.Clamp01(clearLength / shoulderLength);
        }

        // 後ろが狭い場合も横ずれを減らす。
        Vector3 fullOffset = rotation * offset;
        float fullDistance = fullOffset.magnitude;

        if (fullDistance > 0.001f)
        {
            float clearDistance =
                GetClearDistance(pivot, fullOffset, radius);

            allowedShoulder = Mathf.Min(
                allowedShoulder,
                Mathf.Clamp01(clearDistance / fullDistance));
        }

        if (!positionInitialized ||
            allowedShoulder < shoulderAmount ||
            shoulderReturnTime <= 0f)
        {
            shoulderAmount = allowedShoulder;
            shoulderVelocity = 0f;
        }
        else
        {
            shoulderAmount = Mathf.SmoothDamp(
                shoulderAmount,
                allowedShoulder,
                ref shoulderVelocity,
                shoulderReturnTime);
        }

        shoulderAmount =
            Mathf.Clamp(shoulderAmount, 0f, allowedShoulder);

        Vector3 adjustedOffset = new Vector3(
            offset.x * shoulderAmount,
            offset.y * shoulderAmount,
            offset.z);

        Vector3 desiredOffset = rotation * adjustedOffset;
        float desiredDistance = desiredOffset.magnitude;

        if (desiredDistance <= 0.001f)
        {
            Vector3 position = KeepCameraOnPlayerSide(
                originalPivot, pivot, radius);

            transform.SetPositionAndRotation(position, rotation);

            UpdatePlayerVisibility(
                Vector3.Distance(position, originalPivot));

            return;
        }

        Vector3 direction = desiredOffset / desiredDistance;

        float allowedDistance =
            GetClearDistance(pivot, desiredOffset, radius);

        if (!positionInitialized ||
            allowedDistance < currentDistance ||
            distanceReturnTime <= 0f)
        {
            // 壁に近づくときは即座に縮める。
            currentDistance = allowedDistance;
            distanceVelocity = 0f;
        }
        else
        {
            // 壁から離れたときだけ滑らかに戻す。
            currentDistance = Mathf.SmoothDamp(
                currentDistance,
                allowedDistance,
                ref distanceVelocity,
                distanceReturnTime);
        }

        currentDistance =
            Mathf.Clamp(currentDistance, 0f, allowedDistance);

        Vector3 finalPosition =
            pivot + direction * currentDistance;

        // 最終位置が建築に重なった場合は、
        // 安全な回転中心の方向へ短くする。
        if (!IsPositionClear(finalPosition, radius))
        {
            float safe = 0f;
            float blocked = currentDistance;

            for (int i = 0; i < 8; i++)
            {
                float middle = (safe + blocked) * 0.5f;
                Vector3 candidate = pivot + direction * middle;

                if (IsPositionClear(candidate, radius))
                    safe = middle;
                else
                    blocked = middle;
            }

            currentDistance = safe;
            distanceVelocity = 0f;
            finalPosition = pivot + direction * safe;
        }

        // 追加：
        // 元のPlayer位置からも確認し、
        // 間に建築があればPlayer側へカメラを戻す。
        Vector3 correctedPosition = KeepCameraOnPlayerSide(
            originalPivot,
            finalPosition,
            radius);

        if ((correctedPosition - finalPosition).sqrMagnitude > 0.000001f)
        {
            distanceVelocity = 0f;

            // 次のフレームに一気に後ろへ戻らないようにする。
            currentDistance = Mathf.Min(
                currentDistance,
                Vector3.Distance(pivot, correctedPosition));
        }

        finalPosition = correctedPosition;

        transform.SetPositionAndRotation(finalPosition, rotation);

        // 本当に空間がある位置だけを退避位置として記録する。
        if (IsPositionClear(finalPosition, radius))
        {
            lastSafePosition = finalPosition;
            hasSafePosition = true;
        }

        positionInitialized = true;

        UpdatePlayerVisibility(
            Vector3.Distance(finalPosition, originalPivot));
    }

    /// <summary>
    /// Playerとカメラの間に建築が残っていたら、
    /// 最もPlayerに近い障害物の手前へ移動する。
    /// </summary>
    private Vector3 KeepCameraOnPlayerSide(
        Vector3 playerPoint,
        Vector3 cameraPoint,
        float radius)
    {
        Vector3 displacement = cameraPoint - playerPoint;
        float distance = displacement.magnitude;

        if (distance <= 0.001f)
            return cameraPoint;

        Vector3 direction = displacement / distance;
        float safeDistance = distance;

        // 中心から細いRayでも確認する。
        RaycastHit[] rayHits = Physics.RaycastAll(
            playerPoint,
            direction,
            distance,
            collisionLayer,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in rayHits)
        {
            if (!IsObstacle(hit.collider))
                continue;

            safeDistance = Mathf.Min(
                safeDistance,
                Mathf.Max(
                    0f,
                    hit.distance - radius - wallOffset));
        }

        // 開始位置に球を置ける場合は、
        // カメラの幅を含めた経路も再確認する。
        if (IsPositionClear(playerPoint, radius))
        {
            safeDistance = Mathf.Min(
                safeDistance,
                GetClearDistance(playerPoint, displacement, radius));
        }

        return playerPoint + direction * safeDistance;
    }

    private void ApplyFallbackPosition(
        Vector3 playerPoint,
        Quaternion rotation,
        float radius)
    {
        Vector3 candidate = transform.position;

        if (hasSafePosition &&
            IsPositionClear(lastSafePosition, radius))
        {
            candidate = lastSafePosition;
        }

        // 以前の安全な位置とPlayerの間に、
        // 新しく建築された壁がある場合も確認する。
        candidate = KeepCameraOnPlayerSide(
            playerPoint,
            candidate,
            radius);

        transform.SetPositionAndRotation(candidate, rotation);

        positionInitialized = false;
        distanceVelocity = 0f;

        UpdatePlayerVisibility(
            Vector3.Distance(candidate, playerPoint));
    }

    private float GetCollisionRadius()
    {
        float near = playerCamera.nearClipPlane;

        float halfHeight = near *
            Mathf.Tan(
                playerCamera.fieldOfView *
                0.5f * Mathf.Deg2Rad);

        float halfWidth = halfHeight * playerCamera.aspect;

        float nearCornerDistance = Mathf.Sqrt(
            near * near +
            halfHeight * halfHeight +
            halfWidth * halfWidth);

        return Mathf.Max(
            0.01f,
            Mathf.Max(cameraRadius, nearCornerDistance));
    }

    private float GetClearDistance(
        Vector3 origin,
        Vector3 displacement,
        float radius)
    {
        float distance = displacement.magnitude;

        if (distance <= 0.001f)
            return 0f;

        Vector3 direction = displacement / distance;
        float result = distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            direction,
            distance,
            collisionLayer,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (!IsObstacle(hit.collider))
                continue;

            result = Mathf.Min(
                result,
                Mathf.Max(0f, hit.distance - wallOffset));
        }

        return result;
    }

    private bool TryResolvePivot(
        ref Vector3 position,
        float radius)
    {
        if (IsPositionClear(position, radius))
            return true;

        EnsureCollisionProbe();
        collisionProbe.radius = radius;

        Vector3 originalPosition = position;

        for (int iteration = 0; iteration < 8; iteration++)
        {
            Collider[] overlaps = Physics.OverlapSphere(
                position,
                radius,
                collisionLayer,
                QueryTriggerInteraction.Ignore);

            bool moved = false;

            foreach (Collider other in overlaps)
            {
                if (!IsObstacle(other))
                    continue;

                if (Physics.ComputePenetration(
                    collisionProbe,
                    position,
                    Quaternion.identity,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out Vector3 direction,
                    out float distance))
                {
                    Vector3 nextPosition = position + direction *
                        (distance + Mathf.Max(0.005f, wallOffset));

                    if (Vector3.Distance(
                        nextPosition,
                        originalPosition) > maxPivotCorrection)
                    {
                        return false;
                    }

                    // 中心がColliderの外にある場合、
                    // 補正によって壁の反対側へ抜けないよう確認する。
                    Vector3 correction = nextPosition - position;

                    if (HasObstacleOnSegment(position, correction))
                        return false;

                    position = nextPosition;
                    moved = true;
                }
            }

            if (IsPositionClear(position, radius))
                return true;

            if (!moved)
                break;
        }

        return false;
    }

    private bool HasObstacleOnSegment(
        Vector3 origin,
        Vector3 displacement)
    {
        float distance = displacement.magnitude;

        if (distance <= 0.001f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            displacement / distance,
            distance,
            collisionLayer,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (IsObstacle(hit.collider))
                return true;
        }

        return false;
    }

    private void EnsureCollisionProbe()
    {
        if (collisionProbe != null)
            return;

        GameObject probeObject =
            new GameObject("CameraCollisionProbe");

        probeObject.hideFlags = HideFlags.HideAndDontSave;
        probeObject.transform.localScale = Vector3.one;

        collisionProbe =
            probeObject.AddComponent<SphereCollider>();

        collisionProbe.enabled = false;
        collisionProbe.center = Vector3.zero;
    }

    private bool IsPositionClear(
        Vector3 position,
        float radius)
    {
        Collider[] overlaps = Physics.OverlapSphere(
            position,
            radius,
            collisionLayer,
            QueryTriggerInteraction.Ignore);

        foreach (Collider col in overlaps)
        {
            if (IsObstacle(col))
                return false;
        }

        return true;
    }

    private bool IsObstacle(Collider col)
    {
        if (col == null || col == collisionProbe)
            return false;

        // 自分自身と、装備している物のColliderを除外。
        if (playerRoot != null)
        {
            if (col.transform == playerRoot ||
                col.transform.IsChildOf(playerRoot))
            {
                return false;
            }
        }

        // カメラ自身のColliderを除外。
        if (col.transform == transform ||
            col.transform.IsChildOf(transform))
        {
            return false;
        }

        return true;
    }

    private void UpdatePlayerVisibility(float distance)
    {
        if (!hidePlayerWhenClose)
        {
            RestorePlayerRenderers();
            return;
        }

        float threshold = modelHidden
            ? minDistance + showDistanceMargin
            : minDistance;

        if (distance >= threshold)
        {
            RestorePlayerRenderers();
            return;
        }

        modelHidden = true;

        if (playerRenderers == null)
            return;

        foreach (Renderer renderer in playerRenderers)
        {
            if (renderer == null)
                continue;

            // 自分のPlayerに属する見た目だけを隠す。
            if (playerRoot == null ||
                (renderer.transform != playerRoot &&
                 !renderer.transform.IsChildOf(playerRoot)))
            {
                continue;
            }

            if (!savedRendererStates.ContainsKey(renderer))
            {
                savedRendererStates.Add(
                    renderer,
                    renderer.forceRenderingOff);
            }

            renderer.forceRenderingOff = true;
        }
    }

    private void RestorePlayerRenderers()
    {
        foreach (KeyValuePair<Renderer, bool> entry
                 in savedRendererStates)
        {
            if (entry.Key != null)
                entry.Key.forceRenderingOff = entry.Value;
        }

        savedRendererStates.Clear();
        modelHidden = false;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void UpdateCursor()
    {
        if (IsCameraLocked)
        {
            if (!cursorReleased)
            {
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                cursorReleased = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreCursor();
        }
    }

    private void RestoreCursor()
    {
        if (!cursorReleased)
            return;

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        cursorReleased = false;
    }

    private void OnDisable()
    {
        RestoreCursor();
        RestorePlayerRenderers();
        ResetCameraPosition();
    }

    private void OnDestroy()
    {
        RestoreCursor();
        RestorePlayerRenderers();

        if (collisionProbe != null)
            Destroy(collisionProbe.gameObject);
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
                item.GetTransform().position);

            if (distance > PICKUP_DISTANCE)
                return;

            item.RequestPickup(owner);
        }
    }

    public Ray GetCrosshairRay()
    {
        return playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));
    }

    public bool GetCrosshairTarget(out RaycastHit hit)
    {
        if (playerCamera == null)
        {
            hit = default;
            return false;
        }

        return Physics.Raycast(GetCrosshairRay(), out hit);
    }
}
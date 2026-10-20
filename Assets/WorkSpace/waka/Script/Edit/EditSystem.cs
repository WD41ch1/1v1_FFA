using UnityEngine;
using FishNet.Object;

public interface IBuildingEditTarget
{
    Transform EditTransform { get; }

    bool BeginEdit(Camera camera);
    void Paint(Ray ray, bool startStroke);
    bool ConfirmEdit(Transform player, out string reason);
    void ResetSelection();
    void CancelEdit();
}

[DefaultExecutionOrder(-100)]
public class EditSystem : MonoBehaviour
{
    private Camera playerCamera;
    private Transform player;
    private BuildingSystem buildingSystem;

    [Header("編集対象")]
    public LayerMask targetMask = ~0;
    public LayerMask obstructionMask = ~0;
    public float editDistance = 6f;

    [Header("操作キー")]
    public KeyCode editKey = KeyCode.E;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode cancelKey = KeyCode.Escape;

    public bool IsEditing => target != null;

    private INetworkBuildingEditTarget target;
    private NetworkBuildingEdit targetNetwork;

    private bool restoreBuilding;
    private bool waitingForRelease;
    private bool selecting;

    private string message = "";
    private float messageUntil;

    private NetworkObject playerNetworkObject;
    private bool wasLocalOwner;

    private bool CanProcessInput =>
        playerNetworkObject != null &&
        playerNetworkObject.IsClientInitialized &&
        playerNetworkObject.IsOwner;

    private void Awake()
    {
        playerNetworkObject =
            GetComponentInParent<NetworkObject>(true);

        if (playerNetworkObject == null)
        {
            Debug.LogError(
                "EditSystem：親PlayerにNetworkObjectが必要です。",
                this);
            return;
        }

        player = playerNetworkObject.transform;

        buildingSystem =
            player.GetComponentInChildren<BuildingSystem>(true);

        playerCamera =
            player.GetComponentInChildren<Camera>(true);
    }

    public void SetPlayerCamera(Camera camera)
    {
        CancelEditing();
        playerCamera = camera;
    }

    private void ShowMessage(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 4f;
        Debug.LogWarning("[編集] " + text, this);
    }

    private void CancelEditing()
    {
        if (target != null && target.EditTransform != null)
            target.CancelEdit();

        target = null;
        targetNetwork = null;
        selecting = false;

        RestoreBuilding();
    }

    private void Update()
    {
        bool localOwner = CanProcessInput;

        if (!localOwner)
        {
            if (wasLocalOwner)
            {
                CancelEditing();
                playerCamera = null;
            }

            wasLocalOwner = false;
            return;
        }

        wasLocalOwner = true;

        if (playerCamera == null && player != null)
        {
            playerCamera =
                player.GetComponentInChildren<Camera>(true);
        }

        if (waitingForRelease)
        {
            if (!Input.GetMouseButton(0))
                RestoreBuilding();

            return;
        }

        if (playerCamera == null || player == null)
            return;

        if (target != null)
        {
            UpdateEditing();
            return;
        }

        if (Input.GetKeyDown(editKey))
            TryBeginEdit();
    }

    private void UpdateEditing()
    {
        if (target.EditTransform == null ||
            !target.EditTransform.gameObject.activeInHierarchy ||
            !target.IsSelectionActive ||
            targetNetwork == null)
        {
            EndEdit(false);
            return;
        }

        // 編集中にチームなどが変わった場合も確認する。
        if (!targetNetwork.CanPlayerEdit(player))
        {
            ShowMessage("この建築を編集する権限がありません。");
            EndEdit(false);
            return;
        }

        if (Input.GetKeyDown(cancelKey))
        {
            EndEdit(false);
            return;
        }

        if (Input.GetKeyDown(resetKey))
        {
            selecting = false;
            target.ResetSelection();
            TryConfirm();
            return;
        }

        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward);

        if (Input.GetMouseButtonDown(0))
        {
            selecting = true;
            message = "";
            target.Paint(ray, true);
        }
        else if (selecting && Input.GetMouseButton(0))
        {
            target.Paint(ray, false);
        }

        if (selecting && Input.GetMouseButtonUp(0))
        {
            selecting = false;
            TryConfirm();
        }
    }

    private void TryBeginEdit()
    {
        Physics.SyncTransforms();

        Ray aim = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward);

        RaycastHit[] hits = Physics.RaycastAll(
            aim,
            editDistance,
            targetMask.value | obstructionMask.value,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == player ||
                hit.transform.IsChildOf(player))
            {
                continue;
            }

            bool isTargetLayer =
                (targetMask.value &
                 (1 << hit.collider.gameObject.layer)) != 0;

            if (!isTargetLayer)
                break;

            MonoBehaviour[] components =
                hit.collider.GetComponentsInParent<MonoBehaviour>();

            foreach (MonoBehaviour component in components)
            {
                if (component == null || !component.enabled)
                    continue;

                if (!(component is IBuildingEditTarget))
                    continue;

                INetworkBuildingEditTarget candidate =
                    component as INetworkBuildingEditTarget;

                if (candidate == null)
                {
                    ShowMessage(
                        component.GetType().Name +
                        "が古いローカル編集版です。同期対応版が必要です。");
                    return;
                }

                Transform editTransform = candidate.EditTransform;

                if (editTransform == null)
                    continue;

                NetworkBuildingEdit network =
                    editTransform.GetComponentInParent<NetworkBuildingEdit>();

                if (network == null)
                {
                    ShowMessage(
                        "完成建築のルートにNetworkBuildingEditが必要です。");
                    return;
                }

                NetworkPlayerTeam playerTeam =
                    player.GetComponent<NetworkPlayerTeam>();

                if (playerTeam == null)
                {
                    ShowMessage(
                        "PlayerルートにNetworkPlayerTeamが必要です。");
                    return;
                }

                NetworkBuildPiece piece =
                    network.GetComponent<NetworkBuildPiece>();

                if (piece == null)
                {
                    ShowMessage(
                        "完成建築のルートにNetworkBuildPieceが必要です。");
                    return;
                }

                if (string.IsNullOrEmpty(playerTeam.PlayerId) ||
                    string.IsNullOrEmpty(piece.BuilderId))
                {
                    ShowMessage(
                        "PlayerIDまたは建築者IDが未登録・未受信です。");
                    return;
                }

                // 編集画面を開く前に権限を確認。
                if (!network.CanPlayerEdit(player))
                {
                    ShowMessage(
                        "自分または同じチームの建築だけ編集できます。");
                    return;
                }

                if (!network.Ready)
                {
                    ShowMessage(
                        "編集状態の同期が未完了、または前の要求の応答待ちです。");
                    return;
                }

                if (!candidate.BeginEdit(playerCamera))
                {
                    ShowMessage(
                        "編集を開始できません。" +
                        "Grid MaterialとEditの設定を確認してください。");
                    return;
                }

                target = candidate;
                targetNetwork = network;
                selecting = false;
                message = "";

                if (buildingSystem != null)
                {
                    restoreBuilding = buildingSystem.enabled;
                    buildingSystem.CancelBuild();
                    buildingSystem.enabled = false;
                }

                return;
            }

            // 手前の物体を貫通して奥を選ばない。
            break;
        }
    }

    private void TryConfirm()
    {
        if (target == null ||
            target.EditTransform == null ||
            targetNetwork == null)
        {
            EndEdit(false);
            return;
        }

        if (!targetNetwork.CanPlayerEdit(player))
        {
            ShowMessage("この建築を編集する権限がありません。");
            EndEdit(false);
            return;
        }

        float distance = Vector3.Distance(
            player.position,
            target.EditTransform.position);

        if (distance > editDistance + 2f)
        {
            ShowMessage("建築から離れすぎています。");
            return;
        }

        // WallEditからNetworkBuildingEditへ要求が送られる。
        if (target.ConfirmEdit(player, out string reason))
        {
            EndEdit(true);
        }
        else
        {
            ShowMessage(reason);
        }
    }

    private void EndEdit(bool confirmed)
    {
        if (target != null &&
            target.EditTransform != null &&
            !confirmed)
        {
            target.CancelEdit();
        }

        target = null;
        targetNetwork = null;
        selecting = false;
        waitingForRelease = true;
    }

    private void RestoreBuilding()
    {
        if (restoreBuilding && buildingSystem != null)
            buildingSystem.enabled = true;

        restoreBuilding = false;
        waitingForRelease = false;
    }

    private void OnDisable()
    {
        CancelEditing();
        wasLocalOwner = false;
    }

    private void OnGUI()
    {
        if (!CanProcessInput)
            return;

        if (IsEditing)
        {
            GUI.Box(
                new Rect(10, 10, 650, 75),
                "左ドラッグ：選択 / 離す：確定 / " +
                resetKey + "：リセット / " +
                cancelKey + "：キャンセル\n" +
                message);
        }
        else if (!string.IsNullOrEmpty(message) &&
                 Time.unscaledTime < messageUntil)
        {
            GUI.Box(
                new Rect(10, 10, 650, 65),
                message);
        }
    }
}
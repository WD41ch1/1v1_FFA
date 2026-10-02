using UnityEngine;
using FishNet.Object;

// Other building types can implement this interface without changing EditSystem.
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
    public LayerMask targetMask = ~0;
    public LayerMask obstructionMask = ~0;
    public float editDistance = 6f;
    public KeyCode editKey = KeyCode.E;
    public KeyCode resetKey = KeyCode.R;
    public KeyCode cancelKey = KeyCode.Escape;

    public bool IsEditing { get { return target != null; } }
    private IBuildingEditTarget target;
    private bool restoreBuilding;
    private bool waitingForRelease;
    private string message;
    private bool selecting;

    private NetworkObject playerNetworkObject;
    private bool wasLocalOwner;
    private bool CanProcessInput => playerNetworkObject != null &&
        playerNetworkObject.IsClientInitialized && playerNetworkObject.IsOwner;

    private void Awake()
    {
        playerNetworkObject = GetComponentInParent<NetworkObject>(true);
        if (playerNetworkObject == null)
        {
            Debug.LogError("EditSystem: 親PlayerにNetworkObjectが必要です。", this);
            return;
        }
        player = playerNetworkObject.transform;
        buildingSystem = player.GetComponentInChildren<BuildingSystem>(true);
        playerCamera = player.GetComponentInChildren<Camera>(true);
        if (buildingSystem == null)
            Debug.LogError("EditSystem: Player配下にBuildingSystemが必要です。", this);
    }

    public void SetPlayerCamera(Camera camera)
    {
        CancelEditing();
        playerCamera = camera;
    }

    private void CancelEditing()
    {
        if (target != null && target.EditTransform != null) target.CancelEdit();
        target = null;
        selecting = false;
        message = "";
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
            playerCamera = player.GetComponentInChildren<Camera>(true);
        if (waitingForRelease)
        {
            if (!Input.GetMouseButton(0)) RestoreBuilding();
            return;
        }
        if (playerCamera == null || player == null) return;

        if (target != null)
        {
            if (target.EditTransform == null ||
                !target.EditTransform.gameObject.activeInHierarchy)
            {
                EndEdit(false);
                return;
            }
            if (Input.GetKeyDown(cancelKey)) { EndEdit(false); return; }
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Input.GetKeyDown(resetKey))
            {
                selecting = false;
                target.ResetSelection();
                TryConfirm();
                return;
            }
            // Only a click started during editing can trigger confirmation.
            if (Input.GetMouseButtonDown(0))
            {
                selecting = true;
                message = "";
                target.Paint(ray, true);
            }
            else if (selecting && Input.GetMouseButton(0)) target.Paint(ray, false);
            if (selecting && Input.GetMouseButtonUp(0))
            {
                selecting = false;
                TryConfirm();
            }
            return;
        }

        if (!Input.GetKeyDown(editKey)) return;
        Physics.SyncTransforms();
        Ray aim = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(aim, editDistance,
            targetMask.value | obstructionMask.value, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == player || hit.transform.IsChildOf(player)) continue;
            if ((targetMask.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                foreach (MonoBehaviour component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                {
                    IBuildingEditTarget candidate = component as IBuildingEditTarget;
                    if (component.enabled && candidate != null && candidate.BeginEdit(playerCamera))
                    {
                        target = candidate;
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
                }
            }
            break; // Do not select walls through other solid objects.
        }
    }

    private void TryConfirm()
    {
        if (Vector3.Distance(player.position, target.EditTransform.position) > editDistance + 2f)
        { message = "Too far from wall"; return; }
        string reason;
        if (target.ConfirmEdit(player, out reason)) EndEdit(true);
        else message = reason;
    }

    private void EndEdit(bool confirmed)
    {
        if (target != null && target.EditTransform != null && !confirmed) target.CancelEdit();
        target = null;
        selecting = false;
        waitingForRelease = true;
    }

    private void RestoreBuilding()
    {
        if (restoreBuilding && buildingSystem != null) buildingSystem.enabled = true;
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
        if (!CanProcessInput || !IsEditing) return;
        GUI.Box(new Rect(10, 10, 460, 65),
            "LMB drag: Select | Release LMB: Confirm | " + resetKey + ": Reset wall | " + cancelKey + ": Cancel\n" + message);
    }
}

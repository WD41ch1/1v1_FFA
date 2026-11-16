using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;


public abstract class ItemBase : NetworkBehaviour
{
    //  使用者
    protected readonly SyncVar<PlayerManager> syncOwner
        = new SyncVar<PlayerManager>();

    [SerializeField]
    protected InventoryItem itemState;

    /// <summary>
    /// 初期化
    /// </summary>
    public virtual bool Initialize(PlayerManager _owner, ItemData _itemData, ItemView _itemView, InventoryItem _itemState) { return true; }

    public virtual void UsePrimary(InputAction.CallbackContext context) { }
    public virtual void UseSecondary(bool flag) { }
    public virtual void UseReload() { }

    public void SetSyncOwner(ItemBase _script, PlayerManager _myManager)
    {
        if (IsServerInitialized)
            SetSyncOwnerServer(_myManager);
        else
            SetSyncOwnerServerRpc(_myManager);
    }

    [ServerRpc]
    private void SetSyncOwnerServerRpc(PlayerManager _myManager)
    {
        SetSyncOwnerServer(_myManager);
    }
    [Server]
    private void SetSyncOwnerServer(PlayerManager _myManager)
    {
        syncOwner.Value = _myManager;
    }
}

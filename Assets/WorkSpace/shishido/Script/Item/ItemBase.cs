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

    protected readonly SyncVar<InventoryItem> syncInventoryItem
    = new SyncVar<InventoryItem>();

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
            SetSyncOwnerServer(_script, _myManager);
        else
            SetSyncOwnerServerRpc(_script, _myManager);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetSyncOwnerServerRpc(ItemBase _script, PlayerManager _myManager)
    {
        SetSyncOwnerServer(_script, _myManager);
    }
    [Server]
    private void SetSyncOwnerServer(ItemBase _script, PlayerManager _myManager)
    {
        _script.syncOwner.Value = _myManager;
    }

    /// <summary>
    /// SyncInventoryItemの値を設定する
    /// </summary>
    /// <param name="_script"></param>
    /// <param name="_state"></param>
    public void SetSyncInventoryItem(WeaponBase _script, InventoryItem _item)
    {
        if (IsServerInitialized)
            SetSyncInventoryItemServer(_script, _item);
        else
            SetSyncInventoryItemServerRpc(_script, _item);
    }

    [ServerRpc/*(RequireOwnership = false)*/]
    private void SetSyncInventoryItemServerRpc(WeaponBase _script, InventoryItem _item)
    {
        SetSyncInventoryItemServer(_script, _item);
    }
    [Server]
    private void SetSyncInventoryItemServer(WeaponBase _script, InventoryItem _item)
    {
        _script.syncInventoryItem.Value = _item;
    }

}

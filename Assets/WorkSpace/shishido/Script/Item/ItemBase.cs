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
    public virtual bool Initialize(PlayerManager _owner, ItemData _itemData,ItemView _itemView, InventoryItem _itemState) { return true; }

    public virtual void UsePrimary(InputAction.CallbackContext context) { }
    public virtual void UseSecondary(bool flag) { }
    public virtual void UseReload() { }

    [Server]
    public void SetSyncOwner(ItemBase _script,PlayerManager _owner)
    {
        _script.syncOwner.Value = _owner;
    }
}

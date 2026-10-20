using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;


public abstract class ItemBase : MonoBehaviour
{
    //  使用者
    protected PlayerManager owner;

    protected InventoryItem itemState;

    /// <summary>
    /// 初期化
    /// </summary>
    public virtual bool Initialize(PlayerManager _owner, ItemData _itemData, InventoryItem _itemState) { return true; }

    public virtual void UsePrimary(InputAction.CallbackContext context) { }
    public virtual void UseSecondary(bool flag) { }
    public virtual void UseReload() { }

}

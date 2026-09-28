using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;


public abstract class ItemBase : MonoBehaviour
{
    //  使用者
    protected PlayerManager owner;


    /// <summary>
    /// 初期化
    /// </summary>
    public virtual void Initialize(PlayerManager _owner, ItemData ItemData, ItemState state = null) { }

    public virtual void UsePrimary(InputAction.CallbackContext context) { }
    public virtual void UseSecondary(bool flag) { }
    public virtual void UseReload() { }

}

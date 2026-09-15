using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public abstract class ItemBase : MonoBehaviour
{
    //  使用者
    protected PlayerManager owner;

    /// <summary>
    /// 初期化
    /// </summary>
    public virtual void Initialize(PlayerManager _owner,ItemData ItemData) { }

    public virtual void UsePrimary(InputAction.CallbackContext context) { }
    public virtual void UseSecondary(bool flag) { }
    public virtual void UseReload() { }

}

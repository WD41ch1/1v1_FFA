using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public abstract class ItemBase : MonoBehaviour
{
    //  使用者
    protected TC_Character owner;

    /// <summary>
    /// 初期化
    /// </summary>
    public virtual void Initialize(TC_Character _owner,ItemData ItemData) { }

    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
    public virtual void UseReload() { }

}

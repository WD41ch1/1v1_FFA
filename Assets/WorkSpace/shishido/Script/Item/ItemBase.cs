using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    /// <summary>
    /// 初期化
    /// </summary>
    public virtual void Initialize(GameObject owner,ItemData ItemData) { }


    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
    public virtual void UseReload() { }

}

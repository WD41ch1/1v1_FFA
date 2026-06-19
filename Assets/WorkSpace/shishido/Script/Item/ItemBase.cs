using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    //  1スロットに重ねられる個数
    protected int maxStack;

    /// <summary>
    /// 初期化
    /// </summary>
    public virtual void Initialize(ItemData ItemData) { }


    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
    public virtual void UseReload() { }

}

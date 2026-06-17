using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [SerializeField]
    protected WeaponData weaponData;
    [SerializeField]
    protected WeaponView weaponView;

    /// <summary>
    /// 射撃処理
    /// </summary>
    public abstract void Fire();

    /// <summary>
    /// リロード処理
    /// </summary>
    public abstract void Reload();
}

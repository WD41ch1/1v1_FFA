using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponController : MonoBehaviour
{
    protected PlayerManager owner;
    protected WeaponData weaponData;
    protected WeaponView weaponView;
    protected ItemData ItemData;
    protected ItemState weaponState;

    //  リロード中かいなか
    protected bool isReloading;

    public abstract void Initialize(
        PlayerManager _owner,
        ItemData itemData,
        ItemState state = null);

    public abstract void Fire();

    public abstract void Reload();


}

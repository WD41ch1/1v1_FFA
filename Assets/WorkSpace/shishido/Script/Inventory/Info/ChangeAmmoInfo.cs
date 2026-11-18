using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChangeAmmoInfo 
{
    public ChangeAmmoInfo(AmmoType _type, int _amount, ResourceChangeType _changeType)
    {
        type = _type;
        amount = _amount;
        changeType = _changeType;
    }

    public AmmoType type;
    public int amount;
    public ResourceChangeType changeType;
}

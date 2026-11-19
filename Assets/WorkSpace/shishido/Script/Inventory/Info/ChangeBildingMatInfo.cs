using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChangeBildingMatInfo 
{

    public ChangeBildingMatInfo(BildingMatType _type, int _amount, ResourceChangeType _changeType)
    {
        type = _type;
        amount = _amount;
        changeType = _changeType;
    }
    public BildingMatType type;
    public int amount;
    public ResourceChangeType changeType;
}

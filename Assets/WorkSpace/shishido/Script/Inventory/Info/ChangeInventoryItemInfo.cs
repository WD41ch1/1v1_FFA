using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChangeInventoryItemInfo
{
    public ChangeInventoryItemInfo(int _slotNum, ItemData _data, ResourceChangeType _changeType)
    {
        slotNum = _slotNum;
        data = _data;
        changeType = _changeType;
    }

    public int slotNum;
    public ItemData data;

    public ResourceChangeType changeType;
}

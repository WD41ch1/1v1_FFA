using System;
public interface IInventorySlot
{
    Enum GetResourceType();

    ItemType GetItemType(); 
}

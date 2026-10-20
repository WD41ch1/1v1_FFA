using UnityEngine;

public interface IPickupable
{
    void RequestPickup(PlayerManager character);

    Transform GetTransform();
}
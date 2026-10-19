using UnityEngine;
using UnityEngine.TextCore.Text;

public interface IPickupable
{
    void RequestPickup(PlayerManager character);

    Transform GetTransform();
}
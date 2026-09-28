using UnityEngine;
using UnityEngine.TextCore.Text;

public interface IPickupable
{
    void Pickup(PlayerManager character);

    Transform GetTransform();
}
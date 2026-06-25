using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestARProjectile : ProjectileBase
{
    private float speed;
    public override void Initialize(WeaponData data, Transform pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        direction = -pos.forward;
        speed = data.ammoSpeed;
    }

    public override void AmmoBehavior()
    {
        if (rb != null)
            rb.AddForce(direction * speed);
    }
}

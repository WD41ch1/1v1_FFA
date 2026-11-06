using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGProjectile : ProjectileBase
{
    public float pelletFireVel = 1;
    private float speed;

    private bool isInitialize = false;

    public override void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        owner = _owner;
        wBase = _base;
        data = wBase.GetWeaponData();

        speed = data.ammoSpeed;

        isInitialize = true;
    }

    public override void AmmoBehavior()
    {
        if (!isInitialize) return;

        rb.AddForce(transform.forward * speed);

    }

}

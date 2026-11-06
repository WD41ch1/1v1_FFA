using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGProjectile : ProjectileBase
{
    public float pelletFireVel = 1;
    private float speed;

    private bool isInitialize = false;

    public override bool Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;


        if (_owner == null || _base == null)
            return false;

        owner = _owner;
        wBase = _base;


        if (wBase.GetWeaponData() == null)
        {
            return false;
        }
        else
        {
            speed = data.ammoSpeed;
        }

        isInitialize = true;

        //  初期化完了を通知
        return true;
    }

    public override void AmmoBehavior()
    {
        if (!isInitialize) return;

        rb.AddForce(transform.forward * speed);

    }

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGProjectile : ProjectileBase
{
    public float pelletFireVel = 1;
    private float speed;

    public override void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        owner = _owner;
        wBase = _base;
        data = wBase.GetWeaponData();

        speed = data.ammoSpeed;

    }

    public override void AmmoBehavior()
    {

        rb.AddForce(transform.forward * speed);

    }

    public override DamageInfo CreateDamageInfo()
    {
        DamageInfo info = new DamageInfo();

        info.Damage = data.weaponDamage;
        info.Attacker = owner;
        info.Weapon = wBase;
        info.IsHeadshot = false;

        return info;
    }

}

using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SGProjectile : ProjectileBase
{
    public float pelletFireVel = 1;
    private float speed;

    private bool isInitialize = false;

    public override bool Initialize(PlayerManager _owner, WeaponBase _base, WeaponDamageSyncData _syncData, Vector3 pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;


        if (_owner == null || _base == null)
            return false;

        owner = _owner;
        wBase = _base;
        syncOwner.Value = _owner;
        syncWeaponData.Value = _syncData;

        if (!syncWeaponData.Value.IsValid)
        {
            GameDebug.Warning(
                $"[Projectile.Initialize]syncWeaponDataのデータが見つからなかったため" +
                $"初期化できませんでした", isShowLog);
            return false;
        }
        else
        {
            //  弾速をセット
            speed = syncWeaponData.Value.ammoSpeed;
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

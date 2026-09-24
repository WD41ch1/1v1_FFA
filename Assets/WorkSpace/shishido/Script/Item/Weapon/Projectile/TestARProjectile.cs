using System;
using UnityEngine;

public class TestARProjectile : ProjectileBase
{
    private float speed;

    //  テスト用
    private Vector3 previousPosition;

    /// <summary>
    /// 初期化処理
    /// </summary>
    /// <param name="_owner"></param>
    /// <param name="_base"></param>
    /// <param name="pos"></param>
    public override void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        owner = _owner;
        wBase = _base;
        data = wBase.GetWeaponData();

        speed = data.ammoSpeed;

        targetPosition = pos;

        direction =
            (targetPosition - transform.position).normalized;

        transform.forward = direction;

        //  テスト用
        previousPosition = transform.position;
    }

    /// <summary>
    /// 弾の移動
    /// </summary>
    public override void AmmoBehavior()
    {
        //  テスト用
        Vector3 currentPosition = transform.position;
        float moveDistance = speed * Time.deltaTime;
        
        if (moveDistance <= 0f) return;

        // 弾が移動する区間を調べる
        RaycastHit[] hits = Physics.RaycastAll(
            currentPosition,
            direction,
            moveDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        // 手前のものから調べる
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        // すべてのヒットを調べる
        foreach (RaycastHit hit in hits)
        {
            // ヒットしたコライダーを取得
            Collider other = hit.collider;

            // 弾自身は無視
            if (other.transform == transform ||
                other.transform.IsChildOf(transform))
            {
                continue;
            }

            // 射撃者自身は無視
            if (owner != null &&
                (other.transform == owner.transform ||
                 other.transform.IsChildOf(owner.transform)))
            {
                continue;
            }
            // 建築物のHPを持つコンポーネントを取得
            BuildHP buildHP = other.GetComponentInParent<BuildHP>();

            if (buildHP != null)
            {
                // 建築物にヒットしたことを記録
                transform.position = hit.point;

                // ProjectileBaseの建築ダメージ処理を呼ぶ
                OnTriggerEnter(other);
                return;
            }

            // 手前に別の障害物があるなら、
            // その奥の建築物にはダメージを与えない
            break;
        }
        // 弾を移動させる
        transform.position =
            currentPosition + direction * moveDistance;
        //  テスト用
        Debug.DrawLine(
            previousPosition,
            transform.position,
            Color.red,
            1f
        );
        
        previousPosition = transform.position;
    }

    /// <summary>
    /// ヒット時のダメージ情報作成
    /// </summary>
    /// <returns></returns>
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

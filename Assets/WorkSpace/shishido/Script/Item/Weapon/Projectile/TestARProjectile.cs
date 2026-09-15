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
        //transform.position += direction * speed * Time.deltaTime;

        Vector3 currentPosition = transform.position;

        transform.position += direction * speed * Time.deltaTime;

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

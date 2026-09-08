
/// <summary>
/// ダメージ情報
/// </summary>
public struct DamageInfo
{
    //  ダメージ値
    public float Damage;
    //  攻撃してきたPlayer
    public PlayerManager Attacker;
    //  ダメージを与えた武器
    public WeaponBase Weapon;
    //  ヘッドショットかどうか
    public bool IsHeadshot;
}
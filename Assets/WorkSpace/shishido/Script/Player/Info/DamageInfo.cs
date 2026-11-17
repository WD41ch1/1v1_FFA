
/// <summary>
/// ダメージ情報
/// </summary>
public struct DamageInfo
{
    //  ダメージ値
    public float Damage;
    //  ダメージ倍率
    public float DamageMultiplier;
    //  攻撃してきたPlayer
    public PlayerManager Attacker;
    //  ヘッドショットかどうか
    public bool IsHeadshot;
}
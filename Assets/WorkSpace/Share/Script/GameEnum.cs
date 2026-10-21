public enum PlayerType
{
    Player,
    Bot
}

/// <summary>
/// アイテムレアリティ
/// </summary>
public enum ItemRarity
{
    Common,
    UnCommon,
    Rar,
    Epic,
    Legend,
}

/// <summary>
/// アイテムの種類
/// </summary>
public enum ItemType
{
    Item,
    Ammo,
    BildingMat
}

/// <summary>
/// アイテムの種類
/// </summary>
public enum BerItemType
{
    Weapon = 0,         //  武器系
    Healing = 1,        //  回復系
    Throwing = 2,       //  投擲物
}

/// <summary>
/// 武器の射撃モード
/// </summary>
public enum FireMode
{
    FullAuto = 0,   // フルオート
    Burst = 1,      // バースト
    SemiAuto = 2    // 単発
}

/// <summary>
/// 弾の種類
/// </summary>
public enum AmmoType
{
    SmallAmmo = 0,          //  小
    MiddleAmmo = 1,         //  中
    BigAmmo = 2,            //  大
    ShotgunAmmo = 3,        //  散弾
}

/// <summary>
/// 建材の種類
/// </summary>
public enum BildingMatType
{
    Wood,
    Brick,
    Iron,
}

/// <summary>
/// 体の判定部位
/// </summary>
public enum HitPart
{
    Head,       //  頭
    Body,       //  胴体
    Leg         //  足
}


/// <summary>
/// 
/// </summary>
public enum ResourceChangeType
{
    AddedNew,
    Updated,
    Removed
}
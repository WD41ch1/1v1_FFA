
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

public enum BerItemType
{

}

/// <summary>
/// 弾の種類
/// </summary>
public enum AmmoType
{
    SmallAmmo,          //  小
    MiddleAmmo,         //  中
    BigAmmo,            //  大
    ShotgunAmmo,        //  散弾
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
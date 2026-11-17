using System;


[System.Serializable]
public class WeaponState 
{
    // 現在マガジンに入っている弾数
    public int currentAmmo;

    public WeaponState()
    {
        currentAmmo = 0;
    }

    /// <summary>
    ///　初期化
    /// </summary>
    /// <param name="initialAmmo"></param>
    public WeaponState(int initialAmmo)
    {
        currentAmmo = initialAmmo;
    }

    /// <summary>
    /// コピー用コンストラクタ
    /// </summary>
    /// <param name="source"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public WeaponState(WeaponState source) {
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            currentAmmo = source.currentAmmo;
        }
    }
}

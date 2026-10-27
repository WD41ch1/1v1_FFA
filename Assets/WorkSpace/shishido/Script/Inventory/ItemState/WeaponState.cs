using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;


[System.Serializable]
public class WeaponState : ItemState
{
    // 現在マガジンに入っている弾数
    public int currentAmmo;


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

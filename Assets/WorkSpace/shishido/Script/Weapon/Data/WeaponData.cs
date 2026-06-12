using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class WeaponData : ScriptableObject
{
    //  武器名
    public string weaponName;
    //  使用する弾
    public AmmoType ammoType;
    //  武器のダメージ
    public float weaponDamage;
    //  マガジン数
    public int maxAmmo;
    //  距離減衰
    public float distanceAttenuation;
    //  射撃精度
    public float fireAccuracy;
    //  反動(リコイル)
    public float recoilValue;
    //  射撃速度
    public float fireRate;
    //  リロード時間
    public float reloadTime;

    //  ヘッショ倍率
    public float headMultiplier = 2.0f;
    //  胴体倍率
    public float bodyMultiplier = 1.0f;
    //  レッグ倍率
    public float legMultiplier = 0.8f;
}

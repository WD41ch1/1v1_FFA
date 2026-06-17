using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class WeaponData : ScriptableObject
{
    //  武器名
    public string weaponName;
    //  武器のダメージ
    public float weaponDamage;
    //  連射速度
    public float fireRate;
    //  リロード時間
    public float reloadTime;

    //==============================================
    //      弾薬系
    //==============================================
    //  使用する弾
    public AmmoType ammoType;
    //  マガジン数
    public int maxAmmo;

    //==============================================
    //      反動系
    //==============================================
    //  射撃精度
    public float fireAccuracy;
    //  リコイル
    public float recoilValue;

    //==============================================
    //      ダメージ変化系
    //==============================================
    //  距離減衰
    public float distanceAttenuation;
    //  ヘッショ倍率
    public float headMultiplier = 2.0f;
    //  胴体倍率
    public float bodyMultiplier = 1.0f;
    //  レッグ倍率
    public float legMultiplier = 0.8f;
}

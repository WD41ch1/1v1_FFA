using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class WeaponData : ItemData
{
    //==============================================
    //      基本情報
    //==============================================
    [Header("武器のダメージ")]    
    public float weaponDamage;
    [Header("武器の射撃モード")]
    public FireMode fireMode;
    [Header("連射速度")]
    public float fireRate;
    [Header("リロード時間")]
    public float reloadTime;

    //==============================================
    //      弾薬系
    //==============================================
    [Header("使用する弾")] 
    public AmmoType ammoType;
    [Header("マガジン数")] 
    public int maxAmmo;
    [Header("弾速")] 
    public float ammoSpeed;

    //==============================================
    //      反動系
    //==============================================
    [Header("射撃精度(拡散率)")] 
    public float fireAccuracy;
    [Header("リコイル")] 
    public float recoilValue;

    //==============================================
    //      ADS系
    //==============================================
    [Header("覗く速度")]
    public float adsSpeed;
    [Header("ADS倍率")]
    public float adsMultiplier;     //  現状はFOVの数値

    //==============================================
    //      ダメージ変化系
    //==============================================
    [Header("距離減衰")] 
    public float distanceAttenuation;
    [Header("ヘッショ倍率")] 
    public float headMultiplier = 2.0f;
    [Header("胴体倍率")] 
    public float bodyMultiplier = 1.0f;
    [Header("レッグ倍率")] 
    public float legMultiplier = 0.8f;
}

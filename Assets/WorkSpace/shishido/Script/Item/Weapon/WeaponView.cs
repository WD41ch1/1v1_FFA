using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponView : MonoBehaviour
{
    [Header("アイテムの見た目Prefab")]
    public GameObject AppearancePrefab;

    public Animator animator;

    [Header("Point")]//  エフェクト(マズルフラッシュ)、弾の生成位置、レイキャスト開始位置など
    public Transform muzzlePoint;
    //  薬莢などの生成位置
    public Transform shellPoint;

    public Transform adsPoint;

    [Header("Effect")]
    public GameObject muzzleFlashPrefab;

    public GameObject hitEffectPrefab;

    [Header("Projectile")]
    public GameObject projectilePrefab;
}
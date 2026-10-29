using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemView : MonoBehaviour
{
    public Animator animator;


    [Header("武器用:Point")]//  エフェクト(マズルフラッシュ)、弾の生成位置、レイキャスト開始位置など
    public Transform muzzlePoint;
    //  薬莢などの生成位置
    public Transform shellPoint;

    public Transform adsPoint;

    [Header("武器用:Effect")]
    public GameObject muzzleFlashPrefab;

    public GameObject hitEffectPrefab;

    [Header("武器用:Projectile")]
    public GameObject projectilePrefab;
}
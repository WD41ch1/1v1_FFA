using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Rigidbody rb;
    protected Vector3 direction;
    protected Vector3 targetPosition;

    protected PlayerManager owner;
    protected WeaponBase wBase;
    protected WeaponData data;

    //建築へのダメージに必要なもの
    [Header("建築物へのダメージ")]
    [SerializeField, Min(1)]
    private int buildingDamage = 25;

    // 同じ弾が建築物へ何度もダメージを与えないため
    private bool hasHitBuilding;

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="data"></param>
    public abstract void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos);

    /// <summary>
    /// 弾の挙動処理
    /// </summary>
    public abstract void AmmoBehavior();

    public abstract DamageInfo CreateDamageInfo();

    void Update()
    {
        AmmoBehavior();
    }

    public void OnTriggerEnter(Collider other)
    {
        // ===== 追加ここから =====
        //建築物へのダメージ処理
        // すでに建築物にヒットしていたら処理しない
        if (hasHitBuilding) return;
        // 建築物のHPを持つコンポーネントを取得
        BuildHP buildHP = other.GetComponentInParent<BuildHP>();
        // 建築物のHPが存在する場合、ダメージを与える
        if (buildHP != null)
        {
            // 建築物にヒットしたことを記録
            hasHitBuilding = true;
            // ダメージ前のHPを記録
            int beforeHP = buildHP.CurrentHP;
            // ダメージを与える
            buildHP.TakeDamage(buildingDamage);
            Debug.Log(
            $"建築命中：弾ID={GetInstanceID()} " +
            $"建築ID={buildHP.GetInstanceID()} " +
            $"HP {beforeHP} → {buildHP.CurrentHP} " +
            $"ダメージ={buildingDamage}"
);
            // 弾を無効化して削除
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        // ===== 追加ここまで =====

        //  仮のヒット判定
        PlayerManager pm = other.gameObject.GetComponent<PlayerManager>();

        //  NULLもしくは射撃者なら
        if (pm == null || pm == owner) return;

        pm?.playerHealth.TakeDamage(CreateDamageInfo());
    }

}

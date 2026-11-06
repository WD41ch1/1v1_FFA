using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public abstract class ProjectileBase : NetworkBehaviour
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

    #region override・Onchange

    #endregion

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="data"></param>
    public abstract void Initialize(PlayerManager _owner, WeaponBase _base, Vector3 pos);

    /// <summary>
    /// 弾の挙動処理
    /// </summary>
    public abstract void AmmoBehavior();

    void Update()
    {
        AmmoBehavior();
    }

    private HitPart HitPartCheck(Collider col)
    {
        HitPart result = HitPart.Body;

        //  Player出なければ
        if (col.gameObject.tag != "Player")
            return HitPart.Invaild;

        if (col.gameObject.layer == PLAYER_HEAD)
            result = HitPart.Head;
        else if (col.gameObject.layer == PLAYER_BODY)
            result = HitPart.Body;
        else if (col.gameObject.layer == PLAYER_LEG)
            result = HitPart.Leg;
        //  何も取れなかった場合胴体判定にする
        else
            result = HitPart.Body;

        return result;
    }

    public void OnTriggerEnter(Collider other)
    {
        GameDebug.Log(
                $"[OnTriggerEnter] " +
                $"InstanceID:{GetInstanceID()} " +
                $"IsServer:{IsServerInitialized} " +
                $"Owner:{owner}"
            );
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

        if (other.gameObject.tag == PLAYER_TAG)
        {
            if (other.gameObject.layer == PLAYER_MAINCOLLIDER)
                return;

            HitPart hitPart = HitPartCheck(other);

            if (hitPart == HitPart.Invaild)
                return;

            GameObject targetPlayer = other.gameObject;

            //  仮のヒット判定
            PlayerManager target = targetPlayer.GetComponent<PlayerManager>();

            //  NULLもしくは射撃者なら
            if (owner == null ||
                target == null || target== owner) return;

            MatchManager.Instance.RequestTakeDamage(owner, target, hitPart, wBase, data);

        }
    }
}

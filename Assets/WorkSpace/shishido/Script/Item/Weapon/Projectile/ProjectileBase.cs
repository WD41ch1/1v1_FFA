using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static GameConst;

public abstract class ProjectileBase : NetworkBehaviour
{
    [SerializeField, Header("[Debug用]Logの表示切り替え")]
    protected bool isShowLog = false;

    protected Rigidbody rb;
    protected Vector3 direction;
    protected Vector3 targetPosition;

    protected PlayerManager owner;
    protected WeaponBase wBase;
    protected readonly SyncVar<PlayerManager> syncOwner
        = new SyncVar<PlayerManager>(null);
    protected readonly SyncVar<WeaponDamageSyncData> syncWeaponData
        = new SyncVar<WeaponDamageSyncData>();

    //建築へのダメージに必要なもの
    [Header("建築物へのダメージ")]
    [SerializeField, Min(1)]
    private int buildingDamage = 25;

    // 同じ弾が建築物へ何度もダメージを与えないため
    private bool hasHitBuilding;

    #region override・Onchange

    public override void OnStartServer()
    {
        base.OnStartServer();
        syncOwner.OnChange += OnChangeSyncOwner;
    }

    private void OnChangeSyncOwner(PlayerManager prev, PlayerManager next, bool asServer)
    {
        bool prevFlag = false;
        if (prev != null) prevFlag = true;
        else prevFlag = false;

        bool nextFlag = false;
        if (prev != null) nextFlag = true;
        else nextFlag = false;

        GameDebug.Log($"[OnChangeSyncOwner]" +
            $"InstanceID:{GetInstanceID()} " +
            $"prev:{(prevFlag ? prev : "null")}," +
            $"next:{(nextFlag ? next : "null")}",
            isShowLog);
    }
    #endregion

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="data"></param>
    public abstract bool Initialize(PlayerManager _owner, WeaponBase _base, WeaponDamageSyncData _syncData, Vector3 pos);

    /// <summary>
    /// 弾の挙動処理
    /// </summary>
    public abstract void AmmoBehavior();

    void Update()
    {
        AmmoBehavior();
    }

    /// <summary>
    /// 敵へのヒット処理
    /// </summary>
    /// <param name="other"></param>
    private void EnemyHit(Collider other)
    {
        if (other.gameObject.tag == PLAYER_TAG)
        {
            if (syncOwner.Value == null)
                return;

            if (other.gameObject.layer == PLAYER_MAINCOLLIDER)
                return;

            HitPart hitPart = HitPartCheck(other);

            if (hitPart == HitPart.Invaild)
                return;

            GameObject targetPlayer = other.gameObject;

            //  仮のヒット判定
            PlayerManager target = targetPlayer.GetComponentInParent<PlayerManager>();

            //  NULLもしくは射撃者なら
            if (target == null || target == syncOwner.Value)
            {
                return;
            }

#if false

            if (wBase == null || data == null)
            {
                GameDebug.Warning($"[EnemyHit]WeaponBaseもしくはItemDataがNULL" +
                    $"wBase:{wBase},data:{data}");
                return;
            }

            if (MatchManager.Instance != null)
                MatchManager.Instance.RequestTakeDamage(syncOwner.Value, target, hitPart,data);

#else
            if (!syncWeaponData.Value.IsValid)
            {
                GameDebug.Warning($"[EnemyHit]syncWeaponDataのデータがありません", isShowLog);
                return;
            }

            if (MatchManager.Instance != null)
                MatchManager.Instance.RequestTakeDamage(syncOwner.Value, target, hitPart, syncWeaponData.Value);
#endif
        }
    }

    private HitPart HitPartCheck(Collider col)
    {
        HitPart result = HitPart.Body;

        //  Player出なければ
        if (col.gameObject.tag != "Player")
            return HitPart.Invaild;

        int hitLayer = col.gameObject.layer;

        GameDebug.Log($"hitLayer:{hitLayer}," +
            $"Head:{PLAYER_HEAD}," +
            $"BODY:{PLAYER_BODY}," +
            $"LEG:{PLAYER_LEG}",
            isShowLog);

        if (hitLayer == PLAYER_HEAD)
            result = HitPart.Head;
        else if (hitLayer == PLAYER_BODY)
            result = HitPart.Body;
        else if (hitLayer == PLAYER_LEG)
            result = HitPart.Leg;
        //  何も取れなかった場合胴体判定にする
        else
            result = HitPart.Body;

        return result;
    }

    public void OnTriggerEnter(Collider other)
    {
        // 自分の弾を識別するキー
        if (!DoOnce.TryExecute(this))
            return;

        GameDebug.Log(
                $"[OnTriggerEnter] " +
                $"InstanceID:{GetInstanceID()} " +
                $"IsServer:{IsServerInitialized} " +
                $"Owner:{owner}",
                isShowLog
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

        EnemyHit(other);
    }

    private void OnTriggerExit(Collider other)
    {
        DoOnce.Reset(this);
    }

}

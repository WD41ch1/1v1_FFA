using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : nobManagerBase
{
    #region public
    public static MatchManager Instance;

    #endregion

    #region private
    private NetworkManager _networkManager;
    private LobbyManager _lobbyManager;

    private List<NetworkConnection> matchPlayers
    = new List<NetworkConnection>();

    private readonly SyncVar<bool> syncSuccess = new SyncVar<bool>();

    #endregion

    #region Initialize

    private void Awake()
    {
        if (_networkManager == null)
            _networkManager = InstanceFinder.NetworkManager;


        // 既に存在する場合は重複を防ぐ
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        syncSuccess.OnChange += onChangeSuccess;
    }

    private void onChangeSuccess(bool prev, bool next, bool asServer)
    {
        GameDebug.Log($"[onChangeSuccess]:{next}");
    }

    #endregion

    #region 試合管理

    public void StartBattle()
    {
        if (!IsServerInitialized)
            return;

        SceneLoadData sceneLoadData =
            new SceneLoadData("BattleScene");

        InstanceFinder.SceneManager.LoadConnectionScenes(
            sceneLoadData
        );
    }

    public void StartMatch(
        NetworkConnection playerA,
        NetworkConnection playerB)
    {
        if (!IsServerInitialized)
            return;

        matchPlayers.Clear();

        matchPlayers.Add(playerA);
        matchPlayers.Add(playerB);

        SceneLoadData loadData =
            new SceneLoadData("Battle");

        //InstanceFinder.SceneManager.LoadConnectionScenes(
        //    matchPlayers,
        //    loadData
        //);
    }
    #endregion

    #region アイテム取得管理

    /// <summary>
    /// 
    /// </summary>
    /// <param name="character">要求者</param>
    /// <param name="script">対象</param>
    public void RequestPickup(PlayerManager character, PickupBase script)
    {
        if (character == null)
            return;

        if (IsServerInitialized)
        {
            TryPickupOnServer(character, script);
        }
        else if (IsClientInitialized)
        {
            RequestPickupServerRpc(character, script);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPickupServerRpc(
        PlayerManager character,
        PickupBase script,
        NetworkConnection sender = null)
    {
        if (character == null/* || sender == null*/)
            return;

        // 要求したクライアントが、このキャラクターの所有者か確認
        if (character.Owner != sender)
        {
            GameDebug.Warning(
                "[PickupBase] 要求者とキャラクターの所有者が一致しません");
            return;
        }

        TryPickupOnServer(character, script);
    }

    [Server]
    public void TryPickupOnServer(PlayerManager character, PickupBase script)
    {
        if (character == null)
        {
            GameDebug.Warning("[Pickup] character が null");
            return;
        }

        if (!CheckCondition())
        {
            GameDebug.Warning("[Pickup] CheckCondition に失敗");
            return;
        }

        GameDebug.Log(
            $"[Pickup] 取得処理開始: " +
            $"Player={character.name}, " +
            $"IsServer={IsServerInitialized}");

        bool success = script.AddInventory(character);

        syncSuccess.Value = success;
        GameDebug.Log(
            $"[Pickup] AddInventory 結果: {success}");

        if (!success)
            return;

        GameDebug.Log("[Pickup] 追加成功。Despawnを実行");

        script.Despawn();
    }

    /// <summary>
    /// Despawn前の状態チェック
    /// </summary>
    private bool CheckCondition()
    {
        // Server側で初期化されているか
        if (!IsServerInitialized)
        {
            GameDebug.Warning("[Despawn] Serverとして初期化されていません");
            return false;
        }

        // NetworkObjectがSpawn済みか
        if (NetworkObject == null || !NetworkObject.IsSpawned)
        {
            GameDebug.Warning("[Despawn] NetworkObjectが存在しないか、Spawnされていません");
            return false;
        }

        return true;
    }


    #endregion

    #region ダメージ管理

    public void RequestTakeDamage(
        PlayerManager attacker,
        PlayerManager target,
        HitPart hit,
        WeaponDamageSyncData data
        )
    {
        DamageInfo info = CreateDamageInfo(attacker, hit,  data);

        target.playerHealth.TakeDamage(info);
    }

    /// <summary>
    /// ヒット時のダメージ情報作成
    /// </summary>
    /// <returns></returns>
    public DamageInfo CreateDamageInfo(PlayerManager attacker, HitPart hit, WeaponData data)
    {
        DamageInfo info = new DamageInfo();

        info.Damage = data.weaponDamage;
        info.DamageMultiplier =
            SetDamageMultiplier(hit, data, out info.IsHeadshot);
        info.Attacker = attacker;

        return info;
    }
    public DamageInfo CreateDamageInfo(PlayerManager attacker, HitPart hit, WeaponDamageSyncData data)
    {
        DamageInfo info = new DamageInfo();

        info.Damage = data.damage;
        info.DamageMultiplier =
            SetDamageMultiplier(hit, data, out info.IsHeadshot);
        info.Attacker = attacker;

        return info;
    }

    /// <summary>
    /// ダメージ倍率の設定
    /// </summary>
    /// <param name="hit"></param>
    /// <returns></returns>
    private float SetDamageMultiplier(HitPart hit, WeaponData data, out bool headShot)
    {
        float multiplier = 1f;

        headShot = false;

        switch (hit)
        {
            case HitPart.Head:
                multiplier = data.headMultiplier;
                headShot = true;
                break;
            case HitPart.Body:
                multiplier = data.bodyMultiplier;
                break;
            case HitPart.Leg:
                multiplier = data.legMultiplier;
                break;
        }

        return multiplier;
    }
    private float SetDamageMultiplier(HitPart hit, WeaponDamageSyncData data, out bool headShot)
    {
        float multiplier = 1f;

        headShot = false;

        switch (hit)
        {
            case HitPart.Head:
                multiplier = data.headMultiplier;
                headShot = true;
                break;
            case HitPart.Body:
                multiplier = data.bodyMultiplier;
                break;
            case HitPart.Leg:
                multiplier = data.legMultiplier;
                break;
        }

        return multiplier;
    }


    #endregion
}
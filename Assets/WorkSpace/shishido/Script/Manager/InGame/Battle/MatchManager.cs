using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
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

    #region ダメージ管理

    public void RequestTakeDamage(
        PlayerManager attacker,
        PlayerManager target,
        HitPart hit,
        WeaponBase weapon,
        WeaponData data
        )
    {
        DamageInfo info = CreateDamageInfo(attacker, hit, weapon, data);

        target.playerHealth.TakeDamage(info);
    }

    /// <summary>
    /// ヒット時のダメージ情報作成
    /// </summary>
    /// <returns></returns>
    public DamageInfo CreateDamageInfo(PlayerManager attacker, HitPart hit, WeaponBase weapon, WeaponData data)
    {
        DamageInfo info = new DamageInfo();

        info.Damage = data.weaponDamage;
        info.DamageMultiplier =
            SetDamageMultiplier(hit, data, out info.IsHeadshot);
        info.Attacker = attacker;
        info.Weapon = weapon;

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


    #endregion
}
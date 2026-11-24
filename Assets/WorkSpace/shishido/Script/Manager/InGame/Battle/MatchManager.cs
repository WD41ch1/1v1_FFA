using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class MatchManager : nobManagerBase
{
    #region public
    public static MatchManager Instance;

    #endregion

    #region private
    private NetworkManager _networkManager;
    private LobbyManager _lobbyManager;


    private readonly SyncVar<bool> syncSuccess
        = new SyncVar<bool>();

    private readonly SyncList<PlayerManager> deadPlayerList
        = new SyncList<PlayerManager>();

    private HashSet<NetworkConnection> initializedPlayers = new();
    private readonly SyncVar<bool> initialized = new(false);

    [SerializeField]
    private int countDownSeconds = 5;
    private bool isCountingDown = false;

    private CancellationToken destroyToken;


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

        destroyToken = this.GetCancellationTokenOnDestroy();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        syncSuccess.OnChange += onChangeSuccess;
        initialized.OnChange += onChangeInit;
    }

    private void onChangeInit(bool prev, bool next, bool asServer)
    {
        GameDebug.Server("全てのPlayerの初期化が完了");
    }

    private void onChangeSuccess(bool prev, bool next, bool asServer)
    {
        GameDebug.Log($"[onChangeSuccess]:{next}");
    }

    #endregion

    #region 試合管理


    [Server]
    public void AddInitializeComplete(NetworkConnection conn)
    {
        initializedPlayers.Add(conn);

        if (initializedPlayers.Count >= ServerManager.Clients.Count)
        {
            //StartMatch();
            initialized.Value = true;
            StartCountdownAsync(destroyToken).Forget();
        }
    }

    /// <summary>
    /// カウントダウン開始
    /// </summary>
    [Server]
    private async UniTaskVoid StartCountdownAsync(CancellationToken token)
    {
        // 二重実行防止
        if (isCountingDown)
            return;

        isCountingDown = true;

        for (int i = countDownSeconds; i > 0; i--)
        {
            GameDebug.Server($"試合開始まで残り {i} 秒");

            // クライアントに残り時間を通知する処理をここに追加

            await UniTask.Delay(
                1000,
                cancellationToken: token
            );
        }

        GameDebug.Server("試合開始！");

        StartMatch();
    }

    /// <summary>
    /// 試合開始
    /// </summary>
    [Server]
    private void StartMatch()
    {
        // 試合開始処理

    }
    #endregion

    #region 装備配布
    public void GiveMatchDefaultItemRequest(PlayerManager target)
    {
        if (IsServerInitialized)
            _ = GiveMatchDefaultItemServer(target);
        else
            GiveMatchDefaultItemServerRpc(target);
    }

    [ServerRpc(RequireOwnership = false)]
    private void GiveMatchDefaultItemServerRpc(PlayerManager target)
    {
        _ = GiveMatchDefaultItemServer(target);
    }
    [Server]
    public async Task GiveMatchDefaultItemServer(PlayerManager target)
    {
        if (target == null) return;

        if (!ItemDataManager.Instance.IsInitialized)
        {
            if (ItemDataManager.Instance == null)
                return;

            //  ItemDataManagerの初期化が終わるまで待つ
            await UniTask.WaitUntil(
                () => ItemDataManager.Instance.IsInitialized == true
                );
        }

        InventoryManager inventory = target.inventoryManager;

        //  武器
        await AsyncAddItems(target, () => { inventory.AddItem(TryGetItemData(1003)); }, 10);   //  SG
        await AsyncAddItems(target, () => { inventory.AddItem(TryGetItemData(1004)); }, 10);   //  SMG
        await AsyncAddItems(target, () => { inventory.AddItem(TryGetItemData(1001)); }, 10);   //  AR
        //  弾薬           
        await AsyncAddItems(target, () => { inventory.AddAmmo(AmmoType.MiddleAmmo, 500); }, 10);
        await AsyncAddItems(target, () => { inventory.AddAmmo(AmmoType.SmallAmmo, 500); }, 10);
        await AsyncAddItems(target, () => { inventory.AddAmmo(AmmoType.ShotgunAmmo, 100); }, 10);
        //  建材            
        await AsyncAddItems(target, () => { inventory.AddBildMat(BildingMatType.Wood, 500); }, 10);
        await AsyncAddItems(target, () => { inventory.AddBildMat(BildingMatType.Brick, 500); }, 10);
        await AsyncAddItems(target, () => { inventory.AddBildMat(BildingMatType.Iron, 500); }, 10);
    }

    private ItemData TryGetItemData(int itemID)
    {
        ItemData data = null;

        if (!ItemDataManager.Instance.TryGetItemData(
             itemID, out data))
        {
            return null;
        }

        return data;
    }

    private async UniTask AsyncAddItems(PlayerManager target, Action action, int waitFrame = 1)
    {
        if (!DoOnce.TryExecute(target))
            return;

        await UniTask.DelayFrame(waitFrame);

        action?.Invoke();

        await UniTask.DelayFrame(1);

        DoOnce.Reset(target);
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

    #region 死亡状態管理


    /// <summary>
    /// 対象Playerの見た目削除
    /// </summary>
    public void DespawnPalyer(PlayerManager target)
    {
        if (IsServerInitialized) DespawnPalyerServer(target);
        else DespawnPalyerServerRpc(target);
    }
    [ServerRpc(RequireOwnership = false)]
    private void DespawnPalyerServerRpc(PlayerManager target)
    {
        DespawnPalyerServer(target);
    }
    [Server]
    private void DespawnPalyerServer(PlayerManager target)
    {
        // 対象がまだ生きていたら
        if (!target.playerHealth.syncIsDead.Value)
            return;

        target.SetActivPlayerModel(target, false);
        AddDeadList(target);
    }

    public void AddDeadList(PlayerManager target)
    {
        SetsyncDeadPlayerList(target);
    }

    public void SetsyncDeadPlayerList(PlayerManager target)
    {
        if (IsServerInitialized)
            SetsyncDeadPlayerListServer(target);
        else
            SetsyncDeadPlayerListServerRpc(target);

    }
    [ServerRpc]
    private void SetsyncDeadPlayerListServerRpc(PlayerManager target)
    {
        SetsyncDeadPlayerListServer(target);
    }
    [Server]
    private void SetsyncDeadPlayerListServer(PlayerManager target)
    {
        deadPlayerList.Add(target);
    }

    public void RequestRespawn(NetworkConnection target)
    {
        GameDebug.Log($"[RequestRespawn]");

        ScenePlayerSpawner ps
                = FindAnyObjectByType<ScenePlayerSpawner>();

        //  Respawn処理
        TargetPaleryRespawn(target, ps);
    }

    public void TargetPaleryRespawn(NetworkConnection target
        , ScenePlayerSpawner playerSpawner)
    {
        if (IsServerInitialized)
            TargetPaleryRespawnServer(target, playerSpawner);
        else
            TargetPaleryRespawnServerRpc(target, playerSpawner);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TargetPaleryRespawnServerRpc(NetworkConnection target
        , ScenePlayerSpawner playerSpawner)
    {
        TargetPaleryRespawnServer(target, playerSpawner);
    }

    [Server]
    private void TargetPaleryRespawnServer(NetworkConnection target
        , ScenePlayerSpawner playerSpawner)
    {
        playerSpawner?.RespawnPlayer(target);
    }



    public void Respawn(PlayerManager target)
    {
        if (IsServerInitialized)
            RespawnServer(target);
        else
            RespawnServerRpc(target);
    }
    [ServerRpc(RequireOwnership = false)]
    private void RespawnServerRpc(PlayerManager target)
    {
        RespawnServer(target);
    }
    [Server]
    private void RespawnServer(PlayerManager target)
    {
        // ① Inventory・装備を初期化
        //target.inventoryManager.ResetInventory();
        //target.equipmentManager.ResetEquipment();

        // ② HP・シールドを初期化 + 死亡状態を解除
        target.playerHealth.ResetStatus();
        target.playerHealth.SetsyncIsDead(target.playerHealth, false);

        // ③ Respawn地点に移動
        //transform.SetPositionAndRotation(
        //    respawnPosition,
        //    respawnRotation
        //);

        //target.SetActivPlayerModel(target,true);
    }


    #endregion

    #region ダメージ管理

    /// <summary>
    /// ダメージ処理要求
    /// </summary>
    /// <param name="attacker"></param>
    /// <param name="target"></param>
    /// <param name="hit"></param>
    /// <param name="data"></param>
    public void RequestTakeDamage(
        PlayerManager attacker,
        PlayerManager target,
        HitPart hit,
        WeaponDamageSyncData data
        )
    {
        DamageInfo info = CreateDamageInfo(attacker, hit, data);

        DamageResult result
            = target.playerHealth.TakeDamage(target.playerHealth, info);
    }
    //  ピッケル用
    public void RequestTakeDamage(
        PlayerManager attacker,
        PlayerManager target,
        HitPart hit,
        PickaxeData data
        )
    {
        if (attacker == null || target == null || data == null)
            return;

        DamageInfo info = CreateDamageInfo(attacker, hit, data);

        DamageResult result
            = target.playerHealth.TakeDamage(target.playerHealth, info);
    }

    /// <summary>
    /// ヒット時のダメージ情報作成(ピッケル用)
    /// </summary>
    /// <returns></returns>
    public DamageInfo CreateDamageInfo(PlayerManager attacker, HitPart hit, PickaxeData data)
    {
        DamageInfo info = new DamageInfo();

        info.Damage = data.weaponDamage;
        info.DamageMultiplier = 1.0f;
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
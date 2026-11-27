using Cysharp.Threading.Tasks;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Threading;
using UnityEngine;
using static ConversionUtility;
using static GameConst;

public class EquipmentManager : NetworkBehaviour
{
    private PlayerManager myPlayer;

    [SerializeField, Header("アイテム生成場所")]
    private Transform itemSocket;

    [SerializeField, Header("武器アニメーション")]
    private WeaponUpperBodyController weaponUpperBodyController;

    private ItemView currentItemView;

    private InventoryManager subscribedInventory;
    public event Action<int> OnEquipChanged;
    private CancellationToken _createItemToken;
    private CancellationToken _currentProcessToken;

    #region SyncVar
    private readonly SyncVar<int> currentSlot =
        new SyncVar<int>();
    //  現在装備中の武器の見た目モデル
    private readonly SyncVar<GameObject> createItem =
        new SyncVar<GameObject>(null);
    //  現在装備中の武器の処理
    private readonly SyncVar<NetworkObject> currentProcess =
        new SyncVar<NetworkObject>(null);
    //  currentProcessがセット(生成)されたかのフラグ
    private readonly SyncVar<bool> IsCreatedProcess =
        new SyncVar<bool>(false);

    //  同期用ログ格納用変数
    private readonly SyncVar<string> syncDebugLog =
        new SyncVar<string>(string.Empty);
    #endregion

    #region 初期化処理・破棄処理

    private void Awake()
    {
        _createItemToken = this.GetCancellationTokenOnDestroy();
        _currentProcessToken = this.GetCancellationTokenOnDestroy();

        ResolveAnimationController();
    }

    private void ResolveAnimationController()
    {
        if (weaponUpperBodyController != null)
            return;

        weaponUpperBodyController =
            GetComponent<WeaponUpperBodyController>();

        if (weaponUpperBodyController == null)
        {
            weaponUpperBodyController =
                GetComponentInChildren<WeaponUpperBodyController>(true);
        }
    }

    public void RegisterPlayer(PlayerManager player)
    {
        myPlayer = player;
        ResolveAnimationController();
    }

    public void Initialize()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnDeletingSetItems -= DeletingSetItems;
            subscribedInventory = null;
        }

        if (myPlayer == null || myPlayer.inventoryManager == null)
            return;

        subscribedInventory = myPlayer.inventoryManager;
        subscribedInventory.OnDeletingSetItems += DeletingSetItems;
    }


    private void OnDestroy()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnDeletingSetItems -= DeletingSetItems;
        }
    }

    #endregion

    #region 装備処理

    /// <summary>
    /// 指定スロットのアイテムを装備する。
    /// </summary>
    public async UniTask<bool> Equip(int slotNum)
    {
        if (!IsOwner)
            return false;

        if (myPlayer == null || myPlayer.inventoryManager == null)
            return false;

        if (!myPlayer.inventoryManager.IsNumberInSlotsCounts(slotNum))
            return false;

        ItemData data;

        if (slotNum == PICKEL_SLOT)
            data = myPlayer.inventoryManager.pickel;
        else
            data = myPlayer.inventoryManager.GetItem(slotNum);

        if (data == null)
            return false;

        if (!await CreateEquipItem(data, slotNum))
            return false;

        SetCurrentSlot(slotNum);

        // 装備成功後に、そのアイテムのアニメーションへ変更。
        ApplyEquippedAnimation(data);

        // 武器の装備に成功したときだけ鳴らす。
        WeaponBase equippedWeapon = GetcurrentWeapon();

        if (equippedWeapon != null)
            equippedWeapon.PlayEquipSound();

        return true;
    }

    private async UniTask<bool> CreateEquipItem(
        ItemData data,
        int slotNum)
    {
        if (itemSocket == null || data == null ||
            ItemDataManager.Instance == null)
            return false;

        // 必要なデータを確認してから装備を交換する
        if (!ItemDataManager.Instance.TryGetProcessData(
            data.itemId,
            out ItemProcessData processData))
        {
            return false;
        }

        //  ItemDataManagerからの取得データの再確認
        if (processData == null || processData.processPrefab == null)
            return false;

        NetworkBehaviour socketBehaviour = null;
        if (!itemSocket.TryGetComponent<NetworkBehaviour>(out socketBehaviour))
        {
            return false;
        }

        InventoryItem state = null;

        //  ピッケルは除外
        if (slotNum != PICKEL_SLOT)
        {
            //  InventoryItemの状態を取得
            state = myPlayer.inventoryManager.GetInvetoryItem(slotNum);
            if (state == null)
                return false;
        }

        //  すでに装備中のアイテムがあれば外す
        if (GetcurrentItem() != null || createItem.Value != null)
            UnEquip();

        // AppearanceModel生成要求
        RequestCreateAppearanceModel(
            socketBehaviour,
            data.itemId);

        // ProcessData生成要求
        RequestCreateProcessData(
            processData.processPrefab,
            transform,
            myPlayer,
            data.itemId,
            slotNum);


        //  両方の生成完了を同時に待つ
        UniTask<bool> appearanceTask =
            WaitCreatedAppearanceModel(_createItemToken);

        UniTask<bool> processTask =
            WaitChangeIsCreatedProcess(_currentProcessToken);

        // 両方が完了するまで待機
        (bool appearanceSuccess, bool processSuccess) =
            await UniTask.WhenAll(
                appearanceTask,
                processTask);

        //  生成結果を確認
        if (!appearanceSuccess || createItem.Value == null)
        {
            GameDebug.Warning("AppearanceModelの生成に失敗しました。");

            UnEquip();
            return false;
        }

        if (!processSuccess || currentProcess.Value == null)
        {
            GameDebug.Warning("ProcessDataの生成に失敗しました。");

            UnEquip();
            return false;
        }

        //  その他必要なコンポーネント取得
        currentItemView =
            createItem.Value.GetComponentInChildren<ItemView>();
        ItemBase process =
            currentProcess.Value.gameObject.GetComponent<ItemBase>();

        if (process == null || myPlayer == null ||
            data == null || currentItemView == null)
        {
            GameDebug.Warning("[CreateEquipItem] 初期化に必要なデータが不足しています。\n" +
                    NULLchackString(("process", process), ("myPlayer", myPlayer),
                        ("data", data), ("currentItemView", currentItemView)));
            UnEquip();
            return false;
        }

        //  Processの初期化開始
        bool success = process.Initialize(
            myPlayer,
            data,
            currentItemView,
            state);

        return success;

    }

    /// <summary>
    /// ProcessData生成要求
    /// </summary>
    private void RequestCreateProcessData(
        NetworkObject _prefab,
        Transform _trans,
        PlayerManager _myOwner,
        int _itemId,
        int _slotNum)
    {
        CreateProcessData(
            this,
            _prefab,
            _trans,
            _myOwner,
            _itemId,
            _slotNum);
    }
    /// <summary>
    /// ProcessData生成要求
    /// </summary>
    private void RequestCreateAppearanceModel(NetworkBehaviour _socket, int itemId)
    {
        if (IsServerInitialized)
            CreateAppearanceModelServer(this, _socket, itemId);
        else
            CreateAppearanceModelServerRpc(this, _socket, itemId);
    }

    /// <summary>
    /// 装備を外し、通常アニメーションへ戻す。
    /// </summary>
    public void UnEquip()
    {
        ResolveAnimationController();

        if (weaponUpperBodyController != null)
            weaponUpperBodyController.ClearWeapon();

        //  見た目の削除
        DespawnAppearanceModel();
        //  ProcessDataの削除
        RemoveCurrentProcess();

        currentItemView = null;
    }


    public void DeletingSetItems(int slotNum)
    {
        //  削除使用としているスロットが現在装備中のスロットと同じ場合は、装備を外す
        if (slotNum == currentSlot.Value)
        {
            UnEquip();
        }
    }

    public void NextItemEquip()
    {
        if (myPlayer == null || myPlayer.inventoryManager == null)
            return;

        int index = currentSlot.Value;

        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            index = (index + 1) % ITEM_SLOT_MAX;

            if (myPlayer.inventoryManager.GetItem(index) != null)
            {
                _ = Equip(index);
                return;
            }
        }
    }

    public void PreviousItemEquip()
    {
        if (myPlayer == null || myPlayer.inventoryManager == null)
            return;

        int index = currentSlot.Value;

        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            index = (index - 1 + ITEM_SLOT_MAX) % ITEM_SLOT_MAX;

            if (myPlayer.inventoryManager.GetItem(index) != null)
            {
                _ = Equip(index);
                return;
            }
        }
    }

    #endregion

    #region Getter/Setter

    public ItemBase GetcurrentItem()
    {
        if (currentProcess.Value == null)
            return null;

        return currentProcess.Value.GetComponent<ItemBase>();
    }

    public WeaponBase GetcurrentWeapon()
    {
        if (currentProcess.Value == null)
            return null;

        return currentProcess.Value.GetComponent<WeaponBase>();
    }

    public int GetcurrentSlot()
    {
        return currentSlot.Value;
    }

    #endregion

    #region Server関連

    public override void OnStartClient()
    {
        base.OnStartClient();

        syncDebugLog.OnChange += OnchangeLog;
        createItem.OnChange += OnChangecreateitem;
        currentSlot.OnChange += OnChangeEquipItem;
        IsCreatedProcess.OnChange += OnChangeIsCreatedProcess;
        ResolveAnimationController();
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        syncDebugLog.OnChange -= OnchangeLog;
        createItem.OnChange -= OnChangecreateitem;
        currentSlot.OnChange -= OnChangeEquipItem;
        IsCreatedProcess.OnChange -= OnChangeIsCreatedProcess;
    }

    private void OnchangeLog(string prev, string next, bool asServer)
    {
        GameDebug.Log(next);
    }

    private void OnChangecreateitem(GameObject prev, GameObject next, bool asServer)
    {
        GameObject process = next;

        string processInfo = "null";

        if (process != null)
        {
            processInfo =
                $"Name:{process.gameObject.name}";
        }

        GameDebug.Log(
            $"[OnChangecreateitem] " +
            $"ID:{gameObject.name} " +
            $"next:{next} " +
            $"createitem:{processInfo}",
            true
        );

    }

    public void OnChangeEquipItem(
        int prev,
        int next,
        bool asServer)
    {
        OnEquipChanged?.Invoke(next);
        if (OnEquipChanged == null) return;

        //  UIに通知
        OnEquipChanged.Invoke(next);
    }

    private void OnChangeIsCreatedProcess(bool prev, bool next, bool asServer)
    {
        NetworkObject process = currentProcess.Value;

        string processInfo = "null";

        if (process != null)
        {
            processInfo =
                $"Name:{process.gameObject.name}, " +
                $"IsSpawned:{process.IsSpawned}";
        }

        GameDebug.Log(
            $"[OnChangeIsCreatedProcess] " +
            $"ID:{gameObject.name} " +
            $"next:{next} " +
            $"Process:{processInfo}",
            false
        );
    }

    /// <summary>
    /// ProcessData生成
    /// </summary>
    [ServerRpc]
    private void CreateProcessData(
        EquipmentManager _myManager,
        NetworkObject _prefab,
        Transform _trans,
        PlayerManager _owner,
        int _itemId,
        int _slotNum
    )
    {
        if (!IsServerInitialized)
            return;

        if (_myManager == null || _prefab == null)
            return;

        // 既存のProcessDataを削除
        if (_myManager.currentProcess.Value != null)
        {
            NetworkObject oldProcess =
                _myManager.currentProcess.Value;

            if (oldProcess.IsSpawned)
                oldProcess.Despawn();

            _myManager.currentProcess.Value = null;
        }

        // 新規生成
        NetworkObject nob = Instantiate(_prefab, _trans);

        if (nob == null)
            return;

        Spawn(nob);

        // Spawn済みのオブジェクトを登録
        _myManager.currentProcess.Value = nob;

        // 生成完了を通知
        _myManager.IsCreatedProcess.Value = true;

    }

    [ServerRpc]
    private void CreateAppearanceModelServerRpc(
        EquipmentManager _myManager,
        NetworkBehaviour _socket,
        int itemId)
    {
        CreateAppearanceModelServer(_myManager, _socket, itemId);
    }
    /// <summary>
    /// Appearance生成
    /// </summary>
    [Server]
    private void CreateAppearanceModelServer(
        EquipmentManager _myManager,
        NetworkBehaviour _socket,
        int itemId)
    {
        if (_socket == null)
        {
            SyncLog($"[CreateAppearanceModelServer]_socketがNULL");
            return;
        }

        // サーバー側でモデル情報を取得
        if (!ItemDataManager.Instance.TryGetModelData(
            itemId,
            out AppearanceModelData modelData))
        {
            return;
        }

        if (modelData == null ||
            modelData.AppearanceNetPrefab == null)
        {
            return;
        }

        // 既存のモデルを削除
        DespawnAppearanceModelServer(_myManager);

        NetworkObject netPrefab
            = modelData.AppearanceNetPrefab.GetComponent<NetworkObject>();

        // サーバー側のitemSocketを使用して生成
        NetworkObject nob = Instantiate(
            netPrefab,
            _myManager.itemSocket.position,
            _myManager.itemSocket.rotation);

        if (nob == null)
            return;

        // ネットワーク親を設定
        nob.SetParent(_socket);

        // サーバー側でSpawn
        Spawn(nob);

        //  createItemのセット
        //  ※[Server]上なのでSetcreateItemを介さなくともよい
        _myManager.createItem.Value = nob.gameObject;

        SyncLog($"Parent: {nob.transform.parent?.name}");
    }

    public void SetcreateItem(GameObject _item)
    {
        if (IsServerInitialized)
            SetcreateItemServer(this, _item);
        else
            SetcreateItemServerRpc(this, _item);
    }
    [ServerRpc]
    private void SetcreateItemServerRpc(EquipmentManager my, GameObject _item)
    {
        SetcreateItemServer(my, _item);
    }
    [Server]
    private void SetcreateItemServer(EquipmentManager my, GameObject _item)
    {
        my.createItem.Value = _item;
    }

    private void SetIsCreatedProcess(bool _success)
    {
        if (IsServerInitialized)
            SetIsCreatedProcessServer(_success);
        else
            SetIsCreatedProcessServerRpc(_success);
    }
    [ServerRpc]
    private void SetIsCreatedProcessServerRpc(bool _success)
    {
        SetIsCreatedProcessServer(_success);
    }
    [Server]
    private void SetIsCreatedProcessServer(bool _success)
    {
        IsCreatedProcess.Value = _success;
    }

    /// <summary>
    /// CurrentProcessの除去
    /// </summary>
    private void RemoveCurrentProcess()
    {
        if (IsServerInitialized)
            RemoveCurrentProcessServer();
        else
            RemoveCurrentProcessServerRpc();
    }

    /// <summary>
    /// クライアントからサーバーへ要求
    /// </summary>
    [ServerRpc]
    private void RemoveCurrentProcessServerRpc()
    {
        RemoveCurrentProcessServer();
    }

    /// <summary>
    /// 現在の装備処理オブジェクトをサーバーで削除する。
    /// </summary>
    [Server]
    private void RemoveCurrentProcessServer()
    {
        NetworkObject process = currentProcess.Value;

        if (process == null)
            return;

        if (!process.IsSpawned)
        {
            Debug.LogWarning(
                "ProcessがNetworkObjectとしてSpawnされていません。");
            return;
        }

        process.Despawn();
        currentProcess.Value = null;
    }

    private void SetCurrentSlot(int num)
    {
        if (IsServerInitialized)
            SetCurrentSlotServer(this, num);
        else
            SetCurrentSlotServerRpc(this, num);
    }
    [ServerRpc]
    private void SetCurrentSlotServerRpc(EquipmentManager script, int num)
    {
        SetCurrentSlotServer(script, num);
    }
    [Server]
    private void SetCurrentSlotServer(EquipmentManager script, int num)
    {
        script.currentSlot.Value = num;
    }

    public void DespawnAppearanceModel()
    {
        if (IsServerInitialized)
            DespawnAppearanceModelServer(this);
        else
            DespawnAppearanceModelServerRpc(this);
    }
    [ServerRpc]
    private void DespawnAppearanceModelServerRpc(EquipmentManager my)
    {
        DespawnAppearanceModelServer(my);
    }
    [Server]
    private void DespawnAppearanceModelServer(EquipmentManager my)
    {
        if (my == null || my.createItem.Value == null)
            return;

        NetworkObject nob =
            my.createItem.Value.GetComponent<NetworkObject>();

        if (nob != null && nob.IsSpawned)
        {
            nob.Despawn();
        }
        else
        {
            Destroy(my.createItem.Value);
        }

        my.createItem.Value = null;
    }

    public void SyncLog(string log)
    {
        if (IsServerInitialized)
            SyncLogServer(log);
        else
            SyncLogServerServerRpc(log);
    }
    [ServerRpc]
    private void SyncLogServerServerRpc(string log)
    {
        SyncLogServer(log);
    }
    [Server]
    private void SyncLogServer(string log)
    {
        syncDebugLog.Value = log;
    }

    #endregion

    #region UniTask
    private async UniTask<bool> WaitCreatedAppearanceModel(
        CancellationToken destroyToken,
        float timeoutSeconds = 0.2f)
    {
        if (timeoutSeconds <= 0f)
        {
            GameDebug.Error("タイムアウト時間は0より大きくしてください。");
            return false;
        }

        // タイムアウト用トークン
        using var timeoutCts =
            new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        // 破棄とタイムアウトの両方でキャンセル
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                destroyToken,
                timeoutCts.Token);

        //  createItem.Valueに値が入るまで待つ
        try
        {
            await UniTask.WaitUntil(() => createItem.Value == true,
                cancellationToken: linkedCts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            // タイムアウトした場合
            if (timeoutCts.IsCancellationRequested &&
                !destroyToken.IsCancellationRequested)
            {
                GameDebug.Warning(
                    $"createItem.Valueが{timeoutSeconds}秒以内に変更がくわえられませんでした。");
            }

            return false;
        }
    }


    private async UniTask<bool> WaitChangeIsCreatedProcess(
        CancellationToken destroyToken,
        float timeoutSeconds = 0.5f)
    {
        if (timeoutSeconds <= 0f)
        {
            GameDebug.Error("タイムアウト時間は0より大きくしてください。");
            return false;
        }

        // タイムアウト用トークン
        using var timeoutCts =
            new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        // 破棄とタイムアウトの両方でキャンセル
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                destroyToken,
                timeoutCts.Token);

        try
        {
            await UniTask.WaitUntil(() => IsCreatedProcess.Value == true,
                cancellationToken: linkedCts.Token);

            //  生成完了後にフラグを戻す
            SetIsCreatedProcess(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            // タイムアウトした場合
            if (timeoutCts.IsCancellationRequested &&
                !destroyToken.IsCancellationRequested)
            {
                GameDebug.Warning(
                    $"{IsCreatedProcess}が{timeoutSeconds}秒以内に変更がくわえられませんでした。");
            }

            return false;
        }
    }

    #endregion

    #region 追加：アイテムIDごとの装備アニメーション設定

    [Serializable]
    public class ItemAnimationSetting
    {
        [Tooltip("スロット番号ではなく、ItemDataのitemId")]
        public int itemId;

        public WeaponAnimationType animationType =
            WeaponAnimationType.None;
    }

    [Header("追加：アイテムIDごとの構え")]
    [Tooltip("ここに登録したIDは、ItemDataの設定より優先します。")]
    [SerializeField]
    private ItemAnimationSetting[] itemAnimationSettings =
        new ItemAnimationSetting[0];

    [Header("追加：装備アニメーションの確認")]
    [SerializeField]
    private bool logEquipmentAnimation = true;

    /// <summary>
    /// このアイテムに使用するアニメーション種類を取得する。
    /// 射撃・リロード側でも同じ設定を取得できる。
    /// </summary>
    public WeaponAnimationType GetAnimationType(ItemData data)
    {
        if (data == null)
            return WeaponAnimationType.None;

        if (itemAnimationSettings != null)
        {
            foreach (ItemAnimationSetting setting in itemAnimationSettings)
            {
                if (setting != null && setting.itemId == data.itemId)
                    return setting.animationType;
            }
        }

        return data.weaponAnimationType;
    }

    private void ApplyEquippedAnimation(ItemData data)
    {
        if (data == null)
            return;

        ResolveAnimationController();

        if (weaponUpperBodyController == null)
        {
            Debug.LogWarning(
                "[装備アニメーション] " +
                "PlayerにWeaponUpperBodyControllerを設定してください。",
                this);
            return;
        }

        if (!weaponUpperBodyController.isActiveAndEnabled)
        {
            Debug.LogWarning(
                "[装備アニメーション] " +
                "WeaponUpperBodyControllerがOFFです。",
                this);
            return;
        }

        WeaponAnimationType type = GetAnimationType(data);

        if (logEquipmentAnimation)
        {
            Debug.Log(
                $"[装備アニメーション] " +
                $"Player={name} / " +
                $"アイテム={data.ItemName} / " +
                $"ID={data.itemId} / " +
                $"種類={type} / " +
                $"通知先={weaponUpperBodyController.name}",
                this);
        }

        if (GetcurrentItem() is WeaponBase &&
            type == WeaponAnimationType.None)
        {
            Debug.LogWarning(
                $"[装備アニメーション] 武器ID={data.itemId}がNoneです。" +
                "EquipmentManagerの「アイテムIDごとの構え」に登録するか、" +
                "ItemDataのWeapon Animation Typeを設定してください。",
                this);
        }

        weaponUpperBodyController.SetWeaponType(type);
    }

    #endregion
}
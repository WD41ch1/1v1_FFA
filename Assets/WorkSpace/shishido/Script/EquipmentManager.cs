using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine;
using static GameConst;

public class EquipmentManager : NetworkBehaviour
{
    private PlayerManager myPlayer;

    [SerializeField, Header("アイテム生成場所")]
    private Transform itemSocket;

    [SerializeField, Header("武器アニメーション")]
    private WeaponUpperBodyController weaponUpperBodyController;

    private GameObject createItem;
    private ItemView currentItemView;

    private InventoryManager subscribedInventory;
    public event Action<int> OnEquipChanged;

    #region SyncVar
    private readonly SyncVar<int> currentSlot =
    new SyncVar<int>();
    private readonly SyncVar<int> currentItemID =
        new SyncVar<int>();
    private readonly SyncVar<ItemBase> currentItem =
        new SyncVar<ItemBase>();
    private readonly SyncVar<NetworkObject> currentProcess =
        new SyncVar<NetworkObject>(null);
    private readonly SyncVar<bool> IsCreatedProcess =
        new SyncVar<bool>(false);

    #endregion

    private void Awake()
    {
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

    /// <summary>
    /// 指定スロットのアイテムを装備する。
    /// </summary>
    public bool Equip(int slotNum)
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

        if (!CreateEquipItem(myPlayer, data, slotNum))
            return false;

        SetCurrentSlot(this,slotNum);

        // 装備成功後に、そのアイテムのアニメーションへ変更
        ApplyEquippedAnimation(data);

        return true;
    }

    private bool CreateEquipItem(
        PlayerManager owner,
        ItemData data,
        int slotNum)
    {
        if (itemSocket == null || data == null)
            return false;

        if (ItemDataManager.Instance == null)
            return false;

        // 必要なデータを確認してから装備を交換する
        if (!ItemDataManager.Instance.TryGetModelData(
            data.itemId,
            out AppearanceModelData modelData))
        {
            return false;
        }

        if (modelData == null || modelData.AppearancePrefab == null)
            return false;

        if (!ItemDataManager.Instance.TryGetProcessData(
            data.itemId,
            out ItemProcessData processData))
        {
            return false;
        }

        if (processData == null || processData.processPrefab == null)
            return false;

        if (currentItem.Value != null || createItem != null)
            UnEquip();

        //  モデル生成(ローカル)
        createItem = Instantiate(
            modelData.AppearancePrefab,
            itemSocket);
        //  生成ができなかった
        if (createItem == null)
            return false;
        //  モデル情報を取得
        currentItemView = createItem.GetComponent<ItemView>();

        if (currentItemView == null)
        {
            Debug.LogWarning(
                "装備の見た目PrefabにItemViewがありません。",
                this);

            Destroy(createItem);
            createItem = null;
            return false;
        }


        //  内部処理データ生成
        SetcurrentItemInProcess(this, processData.processPrefab);

        if (currentItem.Value == null)
        {
            Destroy(createItem);
            createItem = null;
            currentItemView = null;
            return false;
        }

        //  rocessData生成要求(network)
        RequestCreateProcessData(
            currentItem.Value,
            transform,
            myPlayer,
            data.itemId,
            slotNum
            );

        bool success = IsCreatedProcess.Value;

        GameDebug.Log($"アイテム生成初期化：[{success}]");

        if (!success)
        {
            UnEquip();
            return false;
        }

        return true;
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
    /// 装備を外し、通常アニメーションへ戻す。
    /// </summary>
    public void UnEquip()
    {
        ResolveAnimationController();

        if (weaponUpperBodyController != null)
            weaponUpperBodyController.ClearWeapon();

        if (createItem != null)
        {
            Destroy(createItem);
            createItem = null;
        }

        RemoveCurrentItem();
        RemoveCurrentProcess();

        currentItemView = null;
    }


    public void DeletingSetItems()
    {
        UnEquip();
    }

    public ItemBase GetcurrentItem()
    {
        return currentItem.Value;
    }

    public int GetcurrentSlot()
    {
        return currentSlot.Value;
    }

    public WeaponBase GetcurrentWeapon()
    {
        return currentItem.Value as WeaponBase;
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
                Equip(index);
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
                Equip(index);
                return;
            }
        }
    }

    #region Server関連

    public override void OnStartClient()
    {
        base.OnStartClient();

        currentSlot.OnChange += OnChangeEquipItem;
        ResolveAnimationController();
    }

    public override void OnStopClient()
    {
        currentSlot.OnChange -= OnChangeEquipItem;
        base.OnStopClient();
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

        // サーバー側でデータを取得
        if (!ItemDataManager.Instance.TryGetModelData(
            _itemId, out AppearanceModelData modelData))
            return;

        if (!ItemDataManager.Instance.TryGetProcessData(
            _itemId, out ItemProcessData processData))
            return;

        if (!ItemDataManager.Instance.TryGetItemData(
            _itemId, out ItemData itemData))
            return;

        // 新規生成
        NetworkObject nob = Instantiate(_prefab, _trans);

        Spawn(nob);

        _myManager.currentProcess.Value = nob;

        ItemView viewData =
            modelData.AppearancePrefab.GetComponent<ItemView>();

        InventoryItem state =
            _owner.inventoryManager.GetInvetoryItem(_slotNum);

        bool success = nob.GetComponent<ItemBase>().Initialize(
            _owner,
            itemData,
            viewData,
            state
        );

        TargetEquipResult(Owner, success);
    }

    [TargetRpc]
    private void TargetEquipResult(
    NetworkConnection conn,
    bool success)
    {
        GameDebug.Client($"装備結果：{success}");

        // クライアント側の成功時処理
        SetIsCreatedProcess(success);
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
    /// CurrentItemの除去
    /// </summary>
    private void RemoveCurrentItem()
    {
        if (IsServerInitialized)
            RemoveCurrentItemServer();
        else
            RemoveCurrentItemServerRpc();
    }

    /// <summary>
    /// クライアントからサーバーへ要求
    /// </summary>
    [ServerRpc]
    private void RemoveCurrentItemServerRpc()
    {
        RemoveCurrentItemServer();
    }

    /// <summary>
    /// 現在の装備処理オブジェクトをサーバーで削除する。
    /// </summary>
    [Server]
    private void RemoveCurrentItemServer()
    {
        NetworkObject process = currentItem.Value;

        if (process == null)
            return;

        if (!process.IsSpawned)
        {
            Debug.LogWarning(
                "ProcessがNetworkObjectとしてSpawnされていません。");
            return;
        }

        process.Despawn();
        currentItem.Value = null;
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

    [ServerRpc]
    private void SetcurrentItemInProcess(EquipmentManager script,ItemBase process)
    {
        script.currentItem.Value = process;
    }

    [ServerRpc]
    private void SetCurrentSlot(EquipmentManager script,int num)
    {
        script.currentSlot.Value = num;
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

        if (currentItem.Value is WeaponBase &&
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
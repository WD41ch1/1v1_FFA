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

    private readonly SyncVar<int> currentSlot =
        new SyncVar<int>();

    private readonly SyncVar<int> currentItemID =
        new SyncVar<int>();

    private ItemBase currentItem;
    private GameObject createItem;
    private ItemView currentItemView;

    private InventoryManager subscribedInventory;

    public event Action<int> OnEquipChanged;

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

    private void OnDestroy()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnDeletingSetItems -= DeletingSetItems;
        }
    }

    public void OnChangeEquipItem(
        int prev,
        int next,
        bool asServer)
    {
        OnEquipChanged?.Invoke(next);
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

        ItemData data;

        if (slotNum == PICKEL_SLOT)
            data = myPlayer.inventoryManager.pickel;
        else
            data = myPlayer.inventoryManager.GetItem(slotNum);

        if (data == null)
            return false;

        InventoryItem state =
            myPlayer.inventoryManager.GetInvetoryItem(slotNum);

        if (state == null)
            return false;

        if (!CreateEquipItem(myPlayer, data, state))
            return false;

        currentSlot.Value = slotNum;

        // 装備成功後に、そのアイテムのアニメーションへ変更
        ResolveAnimationController();

        if (weaponUpperBodyController != null)
        {
            weaponUpperBodyController.SetWeaponType(
                data.weaponAnimationType);
        }
        else
        {
            Debug.LogWarning(
                "PlayerにWeaponUpperBodyControllerを設定してください。",
                this);
        }

        return true;
    }

    private bool CreateEquipItem(
        PlayerManager owner,
        ItemData data,
        InventoryItem state)
    {
        if (itemSocket == null || data == null || state == null)
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

        if (currentItem != null || createItem != null)
            UnEquip();

        createItem = Instantiate(
            modelData.AppearancePrefab,
            itemSocket);

        if (createItem == null)
            return false;

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

        currentItem = Instantiate(
            processData.processPrefab,
            transform);

        if (currentItem == null)
        {
            Destroy(createItem);
            createItem = null;
            currentItemView = null;
            return false;
        }

        // 既存の武器生成処理
        Spawn(currentItem);

        bool success = currentItem.Initialize(
            owner,
            data,
            currentItemView,
            state);

        GameDebug.Log($"アイテム生成初期化：[{success}]");

        if (!success)
        {
            UnEquip();
            return false;
        }

        return true;
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

        if (currentItem != null)
            RemoveCurrentItem();

        currentItemView = null;
    }

    /// <summary>
    /// 現在の装備処理オブジェクトをサーバーで削除する。
    /// </summary>
    [Server]
    private void RemoveCurrentItem()
    {
        if (currentItem == null)
            return;

        NetworkObject networkObject =
            currentItem.GetComponent<NetworkObject>();

        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn();
        }
        else
        {
            Debug.LogWarning(
                "武器がNetworkObjectとしてSpawnされていません。");
        }

        currentItem = null;
    }

    public void DeletingSetItems()
    {
        UnEquip();
    }

    public ItemBase GetcurrentItem()
    {
        return currentItem;
    }

    public int GetcurrentSlot()
    {
        return currentSlot.Value;
    }

    public WeaponBase GetcurrentWeapon()
    {
        return currentItem as WeaponBase;
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
}
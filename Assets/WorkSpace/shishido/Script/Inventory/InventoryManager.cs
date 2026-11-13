using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static GameConst;

public class InventoryManager : NetworkBehaviour
{
    [InfoBox("インベントリ内部の処理" +
        "建材、弾、アイテムを管理している場所です")]

    //  収集ツールスロット
    [SerializeField]
    public ItemData pickel;
    [Header("---ドロップオブジェクト---")]
    [SerializeField] public GameObject AmmodropObj;
    [SerializeField] public GameObject MaterialdropObj;
    [SerializeField] public GameObject ItemdropObj;

    //=======================================================
    //              大本の個数管理変数
    //=======================================================

    //  アイテムスロット
    //public List<ItemData> slots /*{ get; private set; } */= new();

    [SerializeField]
    private List<InventoryItem> viewSyncListSlots;

    //  武器スロット本体
    public readonly SyncList<InventoryItem> slots = new();
    private readonly SyncVar<InventoryItem> syncInventoryItem = new();
    private readonly SyncVar<InventoryItem> syncAvailableStackItem = new();
    private readonly SyncVar<InventoryItem> syncUpdateRequestItem = new();

    //  弾薬スロット
    public Dictionary<AmmoType, int> ammoDict =
        new Dictionary<AmmoType, int>();

    //  資材スロット
    public Dictionary<BildingMatType, int> bildMatDict
         = new Dictionary<BildingMatType, int>();

    //=======================================================
    // 結果を受け取るイベント
    public event Action<int, InventoryItem> OnSlotItemReceivedEvent;
    public event Action<int, InventoryItem> OnSlotItemReceivedTaskEvent;

    //EquipmentManager側装備通知処理
    public event Action OnDeletingSetItems;

    //  ホットバーUI側装備通知処理
    public event Action<int> OnRemoveItem;
    public event Action<int, int> OnUpdateItem;

    //  インベントリUI側通知処理
    public event Action<int, ItemData, ResourceChangeType> OnChangeInventoryItem;
    public event Action<int> OnAddHotbarItem;
    public event Action<int, int> OnSwapHotbarItem;
    public event Action<AmmoType, int, ResourceChangeType> OnChangeAmmo;
    public event Action<BildingMatType, int, ResourceChangeType> OnChangeBildMat;

    private void Awake()
    {
        //  アイテムスロットを全て空で登録
        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            slots.Add(null);
            viewSyncListSlots.Add(null);
        }

        OnSlotItemReceivedEvent += OnSlotItemReceived;

    }

    private void OnDestroy()
    {
        OnSlotItemReceivedEvent -= OnSlotItemReceived;
    }

    private void OnSlotItemReceived(int slotNum, InventoryItem data)
    {
        if (data != null)
            GameDebug.Client($"Slot:{slotNum} ItemID:{data.data.itemId}");
        else
            GameDebug.Client($"Slot:{slotNum} NULL");

        SetsyncInventoryItem(data);
        SetsyncAvailableStackItem(data);
        SetsyncUpdateRequestItem(data);
    }

    /// <summary>
    /// 空きがあるスロットの番号取得
    /// </summary>
    /// <returns></returns>
    public bool isAvailableSlot(out int availableSlotNum)
    {
        availableSlotNum = -1;

        for (int i = 0; i < slots.Count; i++)
        {
            //  何も入っていないなら
            if (slots[i] == null)
            {
                availableSlotNum = i;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// スタック可能なアイテムのスロット番号を取得
    /// </summary>
    private bool IsAvailableStackSlot(
    ItemData item,
    out int slotNum)
    {
        slotNum = -1;

        if (item == null || item.MaxStack <= 1)
            return false;

        for (int i = 0; i < slots.Count; i++)
        {
            // 取得要求
            RequestItem(i);
            //  Slot内のデータ取得
            InventoryItem slot = syncAvailableStackItem.Value;


            // 空きスロットなら次へ
            if (slot == null || slot.data == null)
                continue;

            // 違うアイテムなら次へ
            if (slot.data != item)
                continue;

            // スタックに空きがあるなら
            if (slot.stackCount < item.MaxStack)
            {
                slotNum = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// アイテムを拾う（追加）
    /// </summary>
    /// <param name="item">追加するアイテムの設定データ</param>
    /// <param name="state">引き継ぐアイテム状態（任意）</param> 
    public bool AddItem(ItemData item, ItemState state = null)
    {
        // ItemDataのチェック
        if (item == null)
        {
            GameDebug.Warning(
                "[InventoryManager] 追加する ItemData が null です。");
            return false;
        }

        // スタック可能なら、既存スロットへの追加を試みる
        if (item.MaxStack > 1
            && IsAvailableStackSlot(item, out int stackIndex))
        {
            // 取得要求
            RequestItem(stackIndex);
            //  Slot内のデータ取得
            InventoryItem inventoryItem = syncInventoryItem.Value;

            // スタック数を増やす
            inventoryItem.stackCount++;

            // 既存の状態（ItemState）は維持する
            RequestSlotCounterUpdate(stackIndex, item.itemType);

            OnChangeInventoryItem?.Invoke(
                stackIndex,
                item,
                ResourceChangeType.Updated);

            return true;
        }

        // 新しいスロットを探す
        if (!isAvailableSlot(out int availableSlotNum))
        {
            GameDebug.Warning(
                $"[InventoryManager] 空きスロットがありません: {item.ItemName}");
            return false;
        }

        // 新しいInventoryItemを作成
        InventoryItem inventoryItemNew = new InventoryItem(item);

        // ドロップなどから状態を引き継ぐ場合
        if (state != null)
        {
            // 武器には WeaponState が必要
            if (item is WeaponData)
            {
                if (state is WeaponState)
                {
                    inventoryItemNew.state = state;
                }
                else
                {
                    GameDebug.Warning(
                        "[InventoryManager] 武器に WeaponState 以外が渡されました。" +
                        "初期状態を維持します。");
                }
            }
            else
            {
                // 武器以外は ItemState を引き継ぐ
                if (state is ItemState && state is not WeaponState)
                {
                    inventoryItemNew.state = state;
                }
                else
                {
                    GameDebug.Warning(
                        "[InventoryManager] アイテムの状態型が一致しません。" +
                        "初期状態を維持します。");
                }
            }
        }

        // スロットに登録
        RegisterSlot(availableSlotNum, inventoryItemNew);

        return true;
    }

    public void RequestItem(int index)
    {
        if (IsOwner)
        {
            RequestGetSlotItem(this, index);
        }
    }


    #region Server関連

    public override void OnStartServer()
    {
        base.OnStartServer();
        //slots.OnChange += OnchangeSlots;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        slots.OnChange += OnchangeSlots;
    }

    private void OnchangeSlots(SyncListOperation op, int index, InventoryItem oldItem, InventoryItem newItem, bool asServer)
    {
        GameDebug.Log($"[OnchangeSlots]");
        if (index == -1)
            return;
        else if (index <= slots.Count)
        {
            //  インスペクター表示用Listに登録
            viewSyncListSlots[index] = newItem;

            // UIなどへ通知
            OnChangeInventoryItem?.Invoke(
                index,
                newItem.data,
                ResourceChangeType.AddedNew);

            //  ホットバー上のアイコン表示
            OnAddHotbarItem?.Invoke(index);

            //  ホットバーのカウンターアップデート
            RequestSlotCounterUpdate(index, newItem.data.itemType);
        }
        else
            GameDebug.Warning($"[OnchangeSlots]index:{index}");
    }

    [Server]
    private InventoryItem GetSlotItem(InventoryManager myManager, int slotNum)
    {
        return myManager.slots[slotNum];
    }

    [ServerRpc]
    public void RequestGetSlotItem(InventoryManager myManager, int slotNum)
    {
        InventoryItem data = GetSlotItem(myManager, slotNum);

        // 結果を要求元のクライアントに返す
        TargetReceiveSlotItem(Owner, slotNum, data);
    }

    // 要求元のクライアントに結果を返す
    [TargetRpc]
    private void TargetReceiveSlotItem(
        NetworkConnection conn,
        int slotNum,
        InventoryItem data)
    {
        OnSlotItemReceivedEvent?.Invoke(slotNum, data);
    }


    #endregion

    #region SyncVerSetter

    /// <summary>
    /// Slotにアイテムを登録
    /// </summary>
    /// <param name="_registerSlotNum"></param>
    /// <param name="_inventoryItem"></param>
    public void RegisterSlot(
    int _registerSlotNum,
    InventoryItem _inventoryItem)
    {
        if (IsServerInitialized)
            RegisterSlotServer(this, _registerSlotNum, _inventoryItem);
        else
            RegisterSlotServerRpc(this, _registerSlotNum, _inventoryItem);
    }
    [ServerRpc]
    private void RegisterSlotServerRpc(
    InventoryManager myManager,
    int _registerSlotNum,
    InventoryItem _inventoryItem)
    {
        RegisterSlotServer(myManager, _registerSlotNum, _inventoryItem);
    }
    [Server]
    private void RegisterSlotServer(
        InventoryManager myManager,
        int _registerSlotNum,
        InventoryItem _inventoryItem)
    {
        myManager.slots[_registerSlotNum] = _inventoryItem;
    }

    /// <summary>
    /// syncInventoryItemのSetter
    /// </summary>
    private void SetsyncInventoryItem(InventoryItem _item)
    {
        if (IsServerInitialized)
            SetsyncInventoryItemServer(_item);
        else
            SetsyncInventoryItemServerRpc(_item);
    }
    [ServerRpc]
    private void SetsyncInventoryItemServerRpc(InventoryItem _item)
    {
        SetsyncInventoryItemServer(_item);
    }
    [Server]
    private void SetsyncInventoryItemServer(InventoryItem _item)
    {
        syncInventoryItem.Value = _item;
    }

    /// <summary>
    /// syncAvailableStackItemのSetter
    /// </summary>
    private void SetsyncAvailableStackItem(InventoryItem _item)
    {
        if (IsServerInitialized)
            SetsyncAvailableStackItemServer(_item);
        else
            SetsyncAvailableStackItemServerRpc(_item);
    }
    [ServerRpc]
    private void SetsyncAvailableStackItemServerRpc(InventoryItem _item)
    {
        SetsyncAvailableStackItemServer(_item);
    }
    [Server]
    private void SetsyncAvailableStackItemServer(InventoryItem _item)
    {
        syncAvailableStackItem.Value = _item;
    }

    /// <summary>
    /// syncInventoryItemのSetter
    /// </summary>
    private void SetsyncUpdateRequestItem(InventoryItem _item)
    {
        if (IsServerInitialized)
            SetsyncUpdateRequestItemServer(_item);
        else
            SetsyncUpdateRequestItemServerRpc(_item);
    }
    [ServerRpc]
    private void SetsyncUpdateRequestItemServerRpc(InventoryItem _item)
    {
        SetsyncUpdateRequestItemServer(_item);
    }
    [Server]
    private void SetsyncUpdateRequestItemServer(InventoryItem _item)
    {
        syncInventoryItem.Value = _item;
    }


    #endregion

    #region UniTask

    public async UniTask<InventoryItem> AsyncRequestItem(
        InventoryManager myManager,
        int slotNum)
    {
        var tcs = new UniTaskCompletionSource<InventoryItem>();

        // 一時的なイベント登録
        void OnReceived(int index, InventoryItem data)
        {
            if (index != slotNum)
                return;

            OnSlotItemReceivedTaskEvent -= OnReceived;
            tcs.TrySetResult(data);
        }

        OnSlotItemReceivedTaskEvent += OnReceived;

        RequestGetSlotItem(myManager, slotNum);

        return await tcs.Task;
    }

    #endregion

    #region 基本アイテム系

    /// <summary>
    /// InventoryItem取得関数
    /// </summary>
    /// <param name="listNum"></param>
    /// <returns></returns>
    public InventoryItem GetInvetoryItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return null;

        return slots[slotIndex];
    }

    /// <summary>
    /// List内のWeaponState取得関数
    /// </summary>
    /// <param name="listNum"></param>
    /// <returns></returns>
    public ItemState GetItemState(int listNum)
    {
        if (listNum < 0 || listNum >= slots.Count)
            return null;

        return slots[listNum]?.state;
    }

    /// <summary>
    /// 指定したスロットのアイテムを削除する
    /// </summary>
    /// <param name="slotNum">削除するスロット番号</param>
    /// <returns>削除に成功したか</returns>
    public bool RemoveItem(int slotNum)
    {
        // スロット番号の範囲チェック
        if (slotNum < 0 || slotNum >= slots.Count)
        {
            GameDebug.Warning($"[RemoveItem] 無効なスロット番号: {slotNum}");
            return false;
        }

        // 対象スロットのアイテムを取得
        InventoryItem item = slots[slotNum];

        // アイテムが存在しない場合は削除しない
        if (item == null || item.data == null)
        {
            GameDebug.Warning($"[RemoveItem] スロット {slotNum} は空です");
            return false;
        }

        // スロットを空にする
        // RemoveAt() は使わない（後ろのスロットがずれてしまうため）
        slots[slotNum] = null;

        // 必要に応じて、既存のインベントリ更新イベントを呼び出す
        // 例：OnRemoveItem?.Invoke(slotNum);
        //  見た目の削除要求
        OnDeletingSetItems.Invoke();
        //  インベントリ側UIの更新要求
        ItemData data = item.data;
        OnChangeInventoryItem.Invoke(slotNum, data, ResourceChangeType.Removed);
        //  ホットバー側UIの更新要求
        OnRemoveItem.Invoke(slotNum);

        return true;
    }

    /// <summary>
    /// インベントリ内部のアイテムデータを削除
    /// </summary>
    public bool RemoveItem(InventoryItem data)
    {
        //  アイテムからスロット番号を取得
        int number = slots.IndexOf(data);

        // スロット番号の範囲チェック
        if (number < 0 || number >= slots.Count)
        {
            GameDebug.Warning($"[RemoveItem] 無効なスロット番号: {number}");
            return false;
        }

        // 対象スロットのアイテムを取得
        InventoryItem item = slots[number];

        // アイテムが存在しない場合は削除しない
        if (item == null || item.data == null)
        {
            GameDebug.Warning($"[RemoveItem] スロット {number} は空です");
            return false;
        }


        // スロットを空にする
        slots[number] = null;

        //  見た目の削除要求
        OnDeletingSetItems.Invoke();
        //  インベントリ側UIの更新要求
        ItemData itemdata = item.data;
        OnChangeInventoryItem.Invoke(number, itemdata, ResourceChangeType.Removed);
        //  ホットバー側UIの更新要求
        OnRemoveItem.Invoke(number);

        return true;
    }

    /// <summary>
    /// 2つのスロットのアイテムを入れ替える
    /// </summary>
    /// <param name="fromSlotNum">入れ替え元のスロット番号</param>
    /// <param name="toSlotNum">入れ替え先のスロット番号</param>
    /// <returns>入れ替えに成功したか</returns>
    public bool SwapItem(int fromSlotNum, int toSlotNum)
    {
        // 両方のスロット番号をチェック
        if (fromSlotNum < 0 || fromSlotNum >= slots.Count)
        {
            GameDebug.Warning(
                $"[SwapItem] 入れ替え元のスロット番号が無効: {fromSlotNum}");
            return false;
        }

        if (toSlotNum < 0 || toSlotNum >= slots.Count)
        {
            GameDebug.Warning(
                $"[SwapItem] 入れ替え先のスロット番号が無効: {toSlotNum}");
            return false;
        }

        // 同じスロットなら何もしない
        if (fromSlotNum == toSlotNum)
            return true;

        // InventoryItem ごと取得する
        InventoryItem fromItem = slots[fromSlotNum];
        InventoryItem toItem = slots[toSlotNum];

        // アイテムそのものを交換する
        slots[fromSlotNum] = toItem;
        slots[toSlotNum] = fromItem;

        // 必要に応じて、既存のインベントリ更新イベントを呼び出す
        // 例：OnSwapHotbarItem?.Invoke(fromSlotNum, toSlotNum);

        return true;
    }

    /// <summary>
    /// アイテムをドロップする
    /// </summary>
    public bool ItemDroping(
        int number,
        ItemData dropData,
        ItemState state = null)
    {
        // アイテムデータのチェック
        if (dropData == null)
        {
            GameDebug.Warning(
                "[InventoryManager] ドロップする ItemData が null です。");
            return false;
        }

        // ドロップ対象の状態を取得
        ItemState sourceState = state ?? GetItemState(number);

        // 状態をコピー
        ItemState dropState = CopyItemState(sourceState);

        // ドロップオブジェクトを生成
        GameObject obj = Instantiate(
            ItemdropObj,
            transform.position + transform.forward * 5f,
            Quaternion.identity);

        // ItemPickupの初期化
        ItemPickup item = obj.GetComponent<ItemPickup>();

        if (item == null)
        {
            Destroy(obj);

            GameDebug.Warning(
                "[InventoryManager] ItemPickup が見つかりません。");
            return false;
        }

        item.Initialize(dropData, dropState);

        // ネットワーク上にスポーン
        InstanceFinder.ServerManager.Spawn(obj);

        // インベントリからアイテムを削除
        RemoveItem(number);

        // UIなどへ通知
        OnChangeInventoryItem?.Invoke(
            number,
            dropData,
            ResourceChangeType.Removed);

        return true;
    }

    private ItemState CopyItemState(ItemState source)
    {
        if (source == null)
            return null;

        if (source is WeaponState weaponState)
            return new WeaponState(weaponState);

        // 通常の ItemState は、現時点ではコピー方法が不明
        return source;
    }

    public ItemData GetItem(int slotNumber)
    {
        if (slotNumber < 0 || slotNumber >= slots.Count)
            return null;

        return slots[slotNumber]?.data;
    }

    /// <summary>
    /// SlotのUI更新要求
    /// </summary>
    /// <param name="slotNum"></param>
    /// <param name="itemType"></param>
    public void RequestSlotCounterUpdate(
    int slotNum,
    BerItemType itemType)
    {
        if (slotNum < 0 || slotNum >= slots.Count)
            return;

        // 取得要求
        RequestItem(slotNum);
        //  Slot内のデータ取得
        InventoryItem item = syncUpdateRequestItem.Value;

        if (item == null)
            return;

        int count = 0;

        switch (itemType)
        {
            case BerItemType.Weapon:
                if (item.state is WeaponState weaponState)
                    count = weaponState.currentAmmo;
                break;

            case BerItemType.Healing:
            case BerItemType.Throwing:
                count = item.stackCount;
                break;
        }

        OnUpdateItem?.Invoke(slotNum, count);
    }

    public void StartTimerRequest(float duration)
    {

    }

    public bool IsNumberInSlotsCounts(int number)
    {
        bool result = false;
        if (number <= -1)
            result = false;

        if (0 <= number && number <= slots.Count)
            result = true;

        return result;
    }

    #endregion

    #region 共通関数
    /// <summary>
    /// 共通Add関数
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="dict"></param>
    /// <param name="key"></param>
    /// <param name="amount"></param>
    /// <param name="maxValue"></param>
    /// <param name="onOverflow"></param>
    private bool AddResource<TKey>(
    Dictionary<TKey, int> dict,
    TKey key,
    int amount,
    int maxValue,
    Action<TKey, int> onOverflow)
    {
        if (dict == null)
            return false;

        // キーが存在しなければ追加
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, amount);
        }
        else
        {
            dict[key] += amount;
        }

        // 上限チェック
        if (dict[key] > maxValue)
        {
            //  超過分の数を取得
            int overValue = dict[key] - maxValue;
            //  dict[key]の計算はonOverflow内で行われるので省く
            onOverflow?.Invoke(key, overValue);
        }

        return true;
    }
    /// <summary>
    /// 共通Droping関数
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="dict"></param>
    /// <param name="key"></param>
    /// <param name="amount"></param>
    private void DropingResource<TKey>(
    Dictionary<TKey, int> dict,
    TKey key,
    int amount,
    GameObject prefab,
    Transform trans,
    out GameObject obj,
    out int dropAmount)
    {
        //  キーが無ければ
        if (!dict.TryGetValue(key, out int current))
        {
            obj = null;
            dropAmount = 0;
            return;
        }

        dropAmount = Mathf.Min(current, amount);
        dict[key] -= dropAmount;

        if (dropAmount == 0)
        {
            obj = null;
            return;
        }

        //  ドロップオブジェクトを生成
        obj = Instantiate(
            prefab,
            trans.position + trans.forward * 5f,
            Quaternion.identity);
    }

    /// <summary>
    /// 共通資材消費要求
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="dict"></param>
    /// <param name="key"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    private bool TryConsumeResouce<TKey>(
    Dictionary<TKey, int> dict,
    TKey key,
    int amount,
    out int r)
    {
        r = 0;

        // 弾種が存在するか確認
        if (!dict.TryGetValue(key, out int currentAmmo))
        {
            return false;
        }

        // 弾が足りるか確認
        if (currentAmmo < amount)
        {
            //  消費数を残弾数に変更
            amount = currentAmmo;
        }

        // 消費
        dict[key] -= amount;
        r = amount;

        return true;
    }


    /// <summary>
    /// 共通Getter
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="dict"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private int GetResouce<TKey>(
        Dictionary<TKey, int> dict, TKey key)
    {
        if (dict == null)
            return 0;

        // keyが存在するか確認
        if (!dict.TryGetValue(key, out int value))
            return 0;

        return value;
    }

    /// <summary>
    /// 共通Remove関数
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="dict"></param>
    /// <param name="key"></param>
    public void RemoveResouce<TKey>(TKey key)
        where TKey : Enum
    {
        var dict = GetDictionary<TKey>();

        // keyが存在するか確認
        if (dict == null ||
            !dict.TryGetValue(key, out int value))
            return;

        //   Dictionary内のリソースが無くなれば
        if (dict[key] == 0)
            dict.Remove(key);
    }

    /// <summary>
    /// 共通Dictionary取得関数
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <returns></returns>
    private Dictionary<TKey, int> GetDictionary<TKey>()
    where TKey : Enum
    {
        if (typeof(TKey) == typeof(AmmoType))
            return ammoDict as Dictionary<TKey, int>;

        if (typeof(TKey) == typeof(BildingMatType))
            return bildMatDict as Dictionary<TKey, int>;

        return null;
    }

    #endregion

    #region 弾薬系

    public bool AddAmmo(AmmoType type, int amount)
    {
        bool isNew = !ammoDict.ContainsKey(type);

        //  Add共通関数実行
        bool canAddResource = AddResource(
            ammoDict,
            type,
            amount,
            MAX_AMMO,
            AmmoDroping
            );

        if (!canAddResource)
            return false;

        //  新規追加かどうか
        if (isNew)
            OnChangeAmmo?.Invoke(type, ammoDict[type], ResourceChangeType.AddedNew);
        else
            OnChangeAmmo?.Invoke(type, ammoDict[type], ResourceChangeType.Updated);

        return true;
    }

    /// <summary>
    /// 捨てる処理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    public void AmmoDroping(AmmoType type, int amount)
    {
        GameObject obj;
        int dropAmount = 0;

        DropingResource(
        ammoDict,
        type,
        amount,
        AmmodropObj,
        transform,
        out obj,
        out dropAmount);

        //  AmmoPickupを取得
        AmmoPickup ap = obj?.GetComponent<AmmoPickup>();
        ap?.Initialize(type, dropAmount);

        OnChangeAmmo?.Invoke(type, ammoDict[type], ResourceChangeType.Removed);
    }

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeAmmo(AmmoType type, int amount, out int r)
    {
        bool flag;

        if (TryConsumeResouce(ammoDict, type, amount, out r))
        {
            //  UI更新通知
            OnChangeAmmo?.Invoke(type, ammoDict[type],
                ResourceChangeType.Updated);

            flag = true;
        }
        else
        {
            flag = false;
        }

        return flag;
    }

    public int GetAmmo(AmmoType type)
    {
        return GetResouce(ammoDict, type);
    }

    #endregion

    #region 建材系

    public bool AddBildMat(BildingMatType type, int amount)
    {
        if (bildMatDict == null)
            return false;

        //  新規追加かどうか
        bool isNew = !bildMatDict.ContainsKey(type);

        //  Add共通関数実行
        bool canAddResource = AddResource(
            bildMatDict,
            type,
            amount,
            MAX_BILDMAT,
            MatDroping);

        if (!canAddResource)
            return false;

        //  新規追加なら
        if (isNew)
            OnChangeBildMat?.Invoke(type, bildMatDict[type], ResourceChangeType.AddedNew);
        else
            OnChangeBildMat?.Invoke(type, bildMatDict[type], ResourceChangeType.Updated);

        return true;
    }

    /// <summary>
    /// 捨てる処理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    public void MatDroping(BildingMatType type, int amount)
    {
#if true
        DropingResource(
        bildMatDict,
        type,
        amount,
        MaterialdropObj,
        transform,
        out GameObject obj,
        out int dropAmount);

        //  AmmoPickupを取得
        MaterialPickup mp = obj?.GetComponent<MaterialPickup>();
        mp?.Initialize(type, dropAmount);

        OnChangeBildMat?.Invoke(type, bildMatDict[type], ResourceChangeType.Removed);

#endif

    }

    /// <summary>
    /// 建材消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeBildMat(BildingMatType type, int amount, out int r)
    {
        bool flag;

        if (TryConsumeResouce(bildMatDict, type, amount, out r))
        {
            //  UI更新通知
            OnChangeBildMat?.Invoke(type, bildMatDict[type],
                ResourceChangeType.Updated);
            flag = true;
        }
        else
        {
            flag = false;
        }

        return flag;
    }

    public int GetMat(BildingMatType type)
    {
        return GetResouce(bildMatDict, type);
    }

    #endregion

    #region デバッグ用
#if UNITY_EDITOR
    [Button]
    public void ShowDictionary()
    {
        StringBuilder sb = new StringBuilder();

        //  弾
        sb.AppendLine("========弾薬========");
        foreach (var pair in ammoDict)
        {
            sb.AppendLine($"{pair.Key} : {pair.Value}");
        }

        //  建材
        sb.AppendLine("========建材========");
        foreach (var pair in bildMatDict)
        {
            sb.AppendLine($"{pair.Key} : {pair.Value}");
        }

        Debug.Log(sb.ToString());
    }

#endif
    #endregion
}

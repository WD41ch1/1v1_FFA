using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using static GameConst;

public class InventoryManager : MonoBehaviour
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

    private List<InventoryItem> slots = new();


    //  弾薬スロット
    public Dictionary<AmmoType, int> ammoDict =
        new Dictionary<AmmoType, int>();

    //  資材スロット
    public Dictionary<BildingMatType, int> bildMatDict
         = new Dictionary<BildingMatType, int>();

    //=======================================================

    //  武器の状態を保存しておくリスト
    //public List<ItemState> itemStates = new();


    //=======================================================
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
        }
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
            InventoryItem slot = slots[i];

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
            InventoryItem inventoryItem = slots[stackIndex];

            // スタック数を増やす
            inventoryItem.stackCount++;

            // 既存の状態（ItemState）は維持する
            SlotUpdateRequest(stackIndex, item.itemType);

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
        slots[availableSlotNum] = inventoryItemNew;

        // UIなどへ通知
        OnChangeInventoryItem?.Invoke(
            availableSlotNum,
            item,
            ResourceChangeType.AddedNew);

        OnAddHotbarItem?.Invoke(availableSlotNum);

        SlotUpdateRequest(availableSlotNum, item.itemType);

        return true;
    }

    #region 基本アイテム系


    public InventoryItem GetInvetoryItem(int listNum)
    {
        return slots[listNum];
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
    /// アイテムの捨てる処理
    /// </summary>
    public bool ItemDroping(int number, ItemData dropData)
    {
        GameObject obj;

        ItemState state = GetItemState(number);
        RemoveItem(number);

        //  ドロップオブジェクトを生成
        obj = Instantiate(
            ItemdropObj,
            transform.position + transform.forward * 5f,
            Quaternion.identity);

        //  ドロップアイテム(オブジェクト)の初期化
        ItemPickup item = obj.GetComponent<ItemPickup>();
        item.Initialize(dropData, state);

        //  通知
        OnChangeInventoryItem?.Invoke(
            number,
            dropData,
            ResourceChangeType.Removed);

        return true;
    }

    /// <summary>
    /// アイテムの捨てる処理(ステートを新たに設定)
    /// </summary>
    public bool ItemDroping(int number, ItemData dropData, ItemState _state)
    {
        GameObject obj;

        ItemState state = _state;
        //  インベントリ内部のアイテムデータを削除
        RemoveItem(number);

        //  ドロップオブジェクトを生成
        obj = Instantiate(
            ItemdropObj,
            transform.position + transform.forward * 5f,
            Quaternion.identity);

        //  ドロップアイテム(オブジェクト)の初期化
        ItemPickup item = obj.GetComponent<ItemPickup>();
        item.Initialize(dropData, state);

        //  通知
        OnChangeInventoryItem?.Invoke(
            number,
            dropData,
            ResourceChangeType.Removed);

        return true;
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
    public void SlotUpdateRequest(
    int slotNum,
    BerItemType itemType)
    {
        if (slotNum < 0 || slotNum >= slots.Count)
            return;

        InventoryItem item = slots[slotNum];

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
        if(bildMatDict == null)
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

        if(!canAddResource)
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

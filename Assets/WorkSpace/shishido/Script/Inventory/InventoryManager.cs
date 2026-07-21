using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static GameConst;
using static UnityEditor.Progress;

public class InventoryManager : MonoBehaviour
{
    //  収集ツールスロット
    [SerializeField]
    public ItemData pickel;
    [Header("---ドロップオブジェクト---")]
    [SerializeField] public GameObject AmmodropObj;
    [SerializeField] public GameObject MaterialdropObj;
    [SerializeField] public GameObject ItemdropObj;

    //  アイテムスロット
    public List<ItemData> slots /*{ get; private set; } */= new();

    //  弾薬スロット
    public Dictionary<AmmoType, int> ammoDict =
        new Dictionary<AmmoType, int>();

    //  資材スロット
    public Dictionary<BildingMatType, int> bildMatDict
         = new Dictionary<BildingMatType, int>();

    //  ホットバーUI側装備通知処理
    public event Action<int> OnAddItem;
    public event Action<int> OnRemoveItem;

    //  インベントリUI側通知処理
    public event Action<AmmoType, int, ResourceChangeType> OnChangeAmmo;
    public event Action<BildingMatType, int, ResourceChangeType> OnChangeBildMat;

    //  その他
    public DoOnce once = new DoOnce();

    private void Awake()
    {
        //  アイテムスロットを全て空で登録
        for (int i = 0; i < ITEM_SLOT_MAX; i++)
        {
            slots.Add(null);
        }
    }

    #region 基本アイテム系(武器等)
    /// <summary>
    /// アイテムを拾う（追加)
    /// </summary>
    /// <param name="item"></param>
    public void AddItem(ItemData item)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                OnAddItem?.Invoke(i);
                return;
            }
        }
    }

    /// <summary>
    /// アイテムを捨てる
    /// </summary>
    public void RemoveItem(int number)
    {
        slots[number] = null;
        OnRemoveItem.Invoke(number);
    }

    public ItemData GetItem(int slotNumber)
    {
        if (slots == null || slots.Count == 0 ||
            slots.Count < slotNumber + 1)
            return null;

        return slots[slotNumber];
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
    private void AddResource<TKey>(
    Dictionary<TKey, int> dict,
    TKey key,
    int amount,
    int maxValue,
    Action<TKey, int> onOverflow)
    {
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
    int amount)
    {
        // 弾種が存在するか確認
        if (!dict.TryGetValue(key, out int currentAmmo))
        {
            return false;
        }

        // 弾が足りるか確認
        if (currentAmmo < amount)
        {
            return false;
        }

        // 消費
        dict[key] -= amount;

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

    public void AddAmmo(AmmoType type, int amount)
    {
        bool isNew = !ammoDict.ContainsKey(type);

        //  Add共通関数実行
        AddResource(
        ammoDict,
        type,
        amount,
        MAX_AMMO,
        AmmoDroping);

        //  新規追加かどうか
        if (isNew)
            OnChangeAmmo?.Invoke(type, ammoDict[type], ResourceChangeType.AddedNew);
        else
            OnChangeAmmo?.Invoke(type, ammoDict[type], ResourceChangeType.Updated);

    }

    /// <summary>
    /// 捨てる処理
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    public void AmmoDroping(AmmoType type, int amount)
    {
#if true
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
#endif
    }

    /// <summary>
    /// 弾消費要求
    /// </summary>
    /// <param name="type"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public bool TryConsumeAmmo(AmmoType type, int amount)
    {
        bool flag;

        if (TryConsumeResouce(ammoDict, type, amount))
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

    public void AddBildMat(BildingMatType type, int amount)
    {
        bool isNew = !bildMatDict.ContainsKey(type);

        //  Add共通関数実行
        AddResource(
        bildMatDict,
        type,
        amount,
        MAX_BILDMAT,
        MatDroping);

        //  新規追加かどうか
        if (isNew)
            OnChangeBildMat?.Invoke(type, bildMatDict[type], ResourceChangeType.AddedNew);
        else
            OnChangeBildMat?.Invoke(type, bildMatDict[type], ResourceChangeType.Updated);
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
    public bool TryConsumeBildMat(BildingMatType type, int amount)
    {
        bool flag;

        if (TryConsumeResouce(bildMatDict, type, amount))
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

}

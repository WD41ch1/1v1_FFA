using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDataManager : MonoBehaviour
{
    public static ItemDataManager Instance { get; private set; }

    [Header("アイテムの見た目データ")]
    [SerializeField]
    private List<AppearanceModelData> modelDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, AppearanceModelData> modelDataDictionary = new();

    [Header("アイテムの内部処理データ")]
    [SerializeField]
    private List<ItemProcessData> processDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, ItemProcessData> processlDataDictionary = new();

    private void Awake()
    {
        // すでに存在する場合は重複を防ぐ
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーン遷移後も保持する
        DontDestroyOnLoad(gameObject);

        // ID検索用の辞書を構築
        BuildDictionary();
    }


    private void BuildDictionary()
    {
        modelDataDictionary.Clear();
        processlDataDictionary.Clear();

        foreach (AppearanceModelData data in modelDataList)
        {
            if (data == null)
            {
                GameDebug.Warning(
                    "ItemModelManager: ItemModelDataが未登録です。");
                continue;
            }

            if (modelDataDictionary.ContainsKey(data.itemId))
            {
                GameDebug.Error(
                    $"ItemModelManager: ItemModelDataのIDが重複しています: {data.itemId}");
                continue;
            }

            modelDataDictionary.Add(data.itemId, data);
        }

        foreach (ItemProcessData data in processDataList)
        {
            if (data == null || data.processPrefab == null)
            {
                GameDebug.Warning(
                    "ItemModelManager: ItemProcessDataもしくはPrefabが未登録です。");
                continue;
            }

            if (processlDataDictionary.ContainsKey(data.itemId))
            {
                GameDebug.Error(
                    $"ItemModelManager: ItemProcessDataのIDが重複しています: {data.itemId}");
                continue;
            }

            processlDataDictionary.Add(data.itemId, data);
        }
    }

    #region ItemModel


    /// <summary>
    /// IDから見た目データを取得
    /// </summary>
    public bool TryGetModelData(
        int itemId,
        out AppearanceModelData modelData)
    {
        return modelDataDictionary.TryGetValue(
            itemId, out modelData);
    }

    #endregion

    #region ItemProcess

    /// <summary>
    /// IDから内部データを取得
    /// </summary>
    public bool TryGetProcessData(
        int itemId,
        out ItemProcessData modelData)
    {
        return processlDataDictionary.TryGetValue(
            itemId, out modelData);
    }

    #endregion

    private void OnDestroy()
    {
        // 自分自身が登録されている場合のみ解除
        if (Instance == this)
            Instance = null;
    }
}

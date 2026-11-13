using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDataManager : MonoBehaviour
{
    public static ItemDataManager Instance { get; private set; }

    #region ItemData(大本)
    [Header("大本のデータ")]
    [SerializeField]
    private List<ItemData> itemDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, ItemData> itemDataDictionary = new();
    #endregion

    #region モデルSO:武器系
    [Header("アイテムの見た目データ")]
    [SerializeField]
    private List<AppearanceModelData> modelDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, AppearanceModelData> modelDataDictionary = new();
    #endregion

    #region モデルSO:建材
    [Header("建材アイテムの見た目データ")]
    [SerializeField]
    private List<AppearanceModelData> MatmodelDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, AppearanceModelData> MatmodelDataDictionary = new();

    #endregion

    #region 内部処理データ
    [Header("アイテムの内部処理データ")]
    [SerializeField]
    private List<ItemProcessData> processDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, ItemProcessData> processlDataDictionary = new();
    #endregion

    #region アイコンSO:武器系
    [Header("アイテムのアイコンデータ")]
    [SerializeField]
    private List<ItemIconData> iconDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, ItemIconData> iconDataDictionary = new();
    #endregion


    #region Sprite

    [Header("レアリティバックグラウンドカラー")]
    public Sprite rarityBackGroundIcon;

    #endregion

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
        itemDataDictionary.Clear();
        modelDataDictionary.Clear();
        MatmodelDataDictionary.Clear();
        processlDataDictionary.Clear();
        iconDataDictionary.Clear();

        foreach (ItemData data in itemDataList)
        {
            if (data == null)
            {
                GameDebug.Warning(
                    "ItemModelManager: ItemDataが未登録です。");
                continue;
            }

            if (itemDataDictionary.ContainsKey(data.itemId))
            {
                GameDebug.Error(
                    $"ItemModelManager: ItemDataのIDが重複しています: {data.itemId}");
                continue;
            }

            itemDataDictionary.Add(data.itemId, data);
        }

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

        foreach (AppearanceModelData data in MatmodelDataList)
        {
            if (data == null)
            {
                GameDebug.Warning(
                    "ItemModelManager: ItemModelDataが未登録です。");
                continue;
            }

            if (MatmodelDataDictionary.ContainsKey(data.itemId))
            {
                GameDebug.Error(
                    $"ItemModelManager: ItemModelDataのIDが重複しています: {data.itemId}");
                continue;
            }

            MatmodelDataDictionary.Add(data.itemId, data);
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

        foreach (ItemIconData data in iconDataList)
        {
            if (data == null || data.icon == null)
            {
                GameDebug.Warning(
                    "ItemModelManager: ItemIconDataもしくはSpriteが未登録です。");
                continue;
            }

            if (iconDataDictionary.ContainsKey(data.itemId))
            {
                GameDebug.Error(
                    $"ItemModelManager: ItemIconDataのIDが重複しています: {data.itemId}");
                continue;
            }

            iconDataDictionary.Add(data.itemId, data);
        }

    }

    #region ItemData

    /// <summary>
    /// IDから大本データを取得
    /// </summary>
    public bool TryGetItemData(
        int itemId,
        out ItemData itemData)
    {
        return itemDataDictionary.TryGetValue(
            itemId, out itemData);
    }

    #endregion

    #region ItemModel

    /// <summary>
    /// IDから見た目データを取得(武器系)
    /// </summary>
    public bool TryGetModelData(
        int itemId,
        out AppearanceModelData modelData)
    {
        return modelDataDictionary.TryGetValue(
            itemId, out modelData);
    }

    /// <summary>
    /// IDから見た目データを取得(建材系)
    /// </summary>
    public bool TryGetMatModelData(
        int itemId,
        out AppearanceModelData modelData)
    {
        return MatmodelDataDictionary.TryGetValue(
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

    #region ItemIcon

    public bool TryGetItemIconData(
    int itemId,
    out ItemIconData iconData)
    {
        return iconDataDictionary.TryGetValue(
            itemId, out iconData);
    }

    #endregion

    private void OnDestroy()
    {
        // 自分自身が登録されている場合のみ解除
        if (Instance == this)
            Instance = null;
    }
}

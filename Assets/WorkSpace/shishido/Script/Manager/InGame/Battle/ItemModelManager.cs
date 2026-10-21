using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemModelManager : MonoBehaviour
{
    public static ItemModelManager Instance { get; private set; }

    [Header("アイテムの見た目データ")]
    [SerializeField]
    private List<AppearanceModelData> modelDataList = new();

    // IDから見た目データを取得するための辞書
    private Dictionary<int, AppearanceModelData> modelDataDictionary = new();

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

        foreach (AppearanceModelData data in modelDataList)
        {
            if (data == null)
            {
                Debug.LogWarning(
                    "ItemModelManager: ItemModelDataが未登録です。");
                continue;
            }

            if (modelDataDictionary.ContainsKey(data.itemId))
            {
                Debug.LogError(
                    $"ItemModelDataのIDが重複しています: {data.itemId}");
                continue;
            }

            modelDataDictionary.Add(data.itemId, data);
        }
    }

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

    private void OnDestroy()
    {
        // 自分自身が登録されている場合のみ解除
        if (Instance == this)
            Instance = null;
    }
}

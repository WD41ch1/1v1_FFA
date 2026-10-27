
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Excel Importer Makerのデータから
/// ゲーム用ScriptableObjectを作成・更新する。
/// </summary>
public class ItemDataExcelImporter : EditorWindow
{
    [SerializeField] private Entity_Items items;
    [SerializeField] private Entity_Weapons weapons;
    [SerializeField] private Entity_HealingItems healingItems;

    [SerializeField] private DefaultAsset outputFolder;

    [MenuItem("Tools/Item Data/Excel Importer")]
    private static void Open()
    {
        GetWindow<ItemDataExcelImporter>("Item Data Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Excel Importer Makerのデータ",
            EditorStyles.boldLabel);

        items = (Entity_Items)EditorGUILayout.ObjectField(
            "Items", items, typeof(Entity_Items), false);

        weapons = (Entity_Weapons)EditorGUILayout.ObjectField(
            "Weapons", weapons, typeof(Entity_Weapons), false);

        healingItems = (Entity_HealingItems)EditorGUILayout.ObjectField(
            "HealingItems", healingItems,
            typeof(Entity_HealingItems), false);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "出力先", EditorStyles.boldLabel);

        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Data Folder", outputFolder, typeof(DefaultAsset), false);

        EditorGUILayout.HelpBox(
            "Excel生成アセットを指定し、出力先フォルダを選んでください。" +
            "既存のSpriteやPrefabの参照は維持します。",
            MessageType.Info);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(
                   items == null ||
                   weapons == null ||
                   healingItems == null ||
                   outputFolder == null))
        {
            if (GUILayout.Button("ScriptableObjectを一括更新"))
                ImportAll();
        }
    }

    private void ImportAll()
    {
        string folderPath =
            AssetDatabase.GetAssetPath(outputFolder);

        if (string.IsNullOrEmpty(folderPath) ||
            !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError("出力先フォルダを正しく指定してください。");
            return;
        }

        // ExcelデータをIDでまとめる。
        if (!TryBuildDictionary(
                items, weapons, healingItems,
                out var itemRows,
                out var weaponRows,
                out var healingRows))
        {
            return;
        }

        // 既存アセットを読み込む。
        Dictionary<int, ItemData> assets =
            LoadExistingItems(folderPath);

        // 必要な派生クラスが揃っているか事前確認。
        foreach (var pair in itemRows)
        {
            ItemData existing;
            assets.TryGetValue(pair.Key, out existing);

            if (!HasCorrectType(pair.Value, existing))
            {
                Debug.LogError(
                    $"itemId={pair.Key}: 既存アセットの型が" +
                    "ExcelのitemTypeと一致しません。処理を中止します。");
                return;
            }

            // Weapon / Healingの固有データが存在するか確認。
            if (pair.Value.itemType == (int)BerItemType.Weapon &&
                !weaponRows.ContainsKey(pair.Key))
            {
                Debug.LogError(
                    $"itemId={pair.Key}: Weaponsのデータがありません。");
                return;
            }

            if (pair.Value.itemType == (int)BerItemType.Healing &&
                !healingRows.ContainsKey(pair.Key))
            {
                Debug.LogError(
                    $"itemId={pair.Key}: HealingItemsのデータがありません。");
                return;
            }
        }

        int created = 0;
        int updated = 0;

        try
        {
            foreach (var pair in itemRows)
            {
                int id = pair.Key;
                Entity_Items.Param row = pair.Value;

                if (!assets.TryGetValue(id, out ItemData data))
                {
                    data = CreateItemData(row);
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{folderPath}/Item_{id}.asset");

                    AssetDatabase.CreateAsset(data, path);
                    assets.Add(id, data);
                    created++;
                }
                else
                {
                    updated++;
                }

                // 共通情報を更新する。
                data.itemId = row.itemId;
                data.ItemName = row.itemName;
                data.itemType = (BerItemType)row.itemType;
                data.maxStack = Mathf.Max(1, row.maxStack);

                // 武器固有情報を更新する。
                if (data is WeaponData weapon)
                {
                    var w = weaponRows[id];

                    weapon.weaponDamage = w.weaponDamage;
                    weapon.fireMode = (FireMode)w.FireMode;
                    weapon.fireRate = w.fireRate;
                    weapon.reloadTime = w.reloadTime;
                    weapon.ammoType = (AmmoType)w.ammoType;
                    weapon.magazineSize = w.magazineSize;
                    weapon.ammoSpeed = w.ammoSpeed;
                    weapon.fireAccuracy = w.fireAccuracy;
                    weapon.recoilValue = w.recoilValue;
                    weapon.adsSpeed = w.adsSpeed;
                    weapon.adsMultiplier = w.adsMultiplier;
                    weapon.distanceAttenuation = w.distanceAttenuation;
                    weapon.headMultiplier = w.headMultiplier;
                    weapon.bodyMultiplier = w.bodyMultiplier;
                    weapon.legMultiplier = w.legMultiplier;
                }

                // 回復アイテム固有情報を更新する。
                if (data is HealingItemData healing)
                {
                    var h = healingRows[id];

                    healing.healingValue = h.healingValue;
                    healing.maxHealingAmount = h.maxHealingAmount;
                    healing.healingTiem = h.healingTiem;
                    healing.healingCoolTiem = h.healingCoolTiem;
                }

                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"アイテムデータ更新完了。新規作成: {created}、更新: {updated}");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    private static ItemData CreateItemData(Entity_Items.Param row)
    {
        switch ((BerItemType)row.itemType)
        {
            case BerItemType.Weapon:
                return CreateInstance<WeaponData>();

            case BerItemType.Healing:
                return CreateInstance<HealingItemData>();

            case BerItemType.Throwing:
                return CreateInstance<ItemData>();

            default:
                throw new System.ArgumentOutOfRangeException(
                    nameof(row.itemType), row.itemType,
                    "未定義のBerItemTypeです。");
        }
    }

    private static bool HasCorrectType(
        Entity_Items.Param row, ItemData existing)
    {
        if (existing == null)
            return true;

        switch ((BerItemType)row.itemType)
        {
            case BerItemType.Weapon:
                return existing is WeaponData;

            case BerItemType.Healing:
                return existing is HealingItemData;

            case BerItemType.Throwing:
                return existing.GetType() == typeof(ItemData);

            default:
                return false;
        }
    }

    private static bool TryBuildDictionary(
        Entity_Items itemEntity,
        Entity_Weapons weaponEntity,
        Entity_HealingItems healingEntity,
        out Dictionary<int, Entity_Items.Param> itemRows,
        out Dictionary<int, Entity_Weapons.Param> weaponRows,
        out Dictionary<int, Entity_HealingItems.Param> healingRows)
    {
        itemRows = new Dictionary<int, Entity_Items.Param>();
        weaponRows = new Dictionary<int, Entity_Weapons.Param>();
        healingRows = new Dictionary<int, Entity_HealingItems.Param>();

        foreach (var sheet in itemEntity.sheets)
            foreach (var row in sheet.list)
            {
                if (row == null ||
                    row.itemId <= 0 ||
                    !System.Enum.IsDefined(typeof(BerItemType), row.itemType))
                {
                    Debug.LogError("Itemsに不正なIDまたはitemTypeがあります。");
                    return false;
                }

                if (itemRows.ContainsKey(row.itemId))
                {
                    Debug.LogError($"ItemsでIDが重複しています: {row.itemId}");
                    return false;
                }

                itemRows.Add(row.itemId, row);
            }

        foreach (var sheet in weaponEntity.sheets)
            foreach (var row in sheet.list)
            {
                if (row == null || row.itemId <= 0 ||
                    !System.Enum.IsDefined(typeof(FireMode), row.FireMode) ||
                    !System.Enum.IsDefined(typeof(AmmoType), row.ammoType))
                {
                    Debug.LogError("Weaponsに不正なIDまたはenum値があります。");
                    return false;
                }

                if (weaponRows.ContainsKey(row.itemId))
                {
                    Debug.LogError($"WeaponsでIDが重複しています: {row.itemId}");
                    return false;
                }

                weaponRows.Add(row.itemId, row);
            }

        foreach (var sheet in healingEntity.sheets)
            foreach (var row in sheet.list)
            {
                if (row == null || row.itemId <= 0)
                {
                    Debug.LogError("HealingItemsに不正なIDがあります。");
                    return false;
                }

                if (healingRows.ContainsKey(row.itemId))
                {
                    Debug.LogError(
                        $"HealingItemsでIDが重複しています: {row.itemId}");
                    return false;
                }

                healingRows.Add(row.itemId, row);
            }

        // 固有データがItems側の正しい種類に紐づいているか検証。
        foreach (var pair in weaponRows)
        {
            if (!itemRows.TryGetValue(pair.Key, out var item) ||
                item.itemType != (int)BerItemType.Weapon)
            {
                Debug.LogError(
                    $"WeaponsのitemId={pair.Key}に対応するWeaponのItems行がありません。");
                return false;
            }
        }

        foreach (var pair in healingRows)
        {
            if (!itemRows.TryGetValue(pair.Key, out var item) ||
                item.itemType != (int)BerItemType.Healing)
            {
                Debug.LogError(
                    $"HealingItemsのitemId={pair.Key}に対応するHealingのItems行がありません。");
                return false;
            }
        }

        return true;
    }

    private static Dictionary<int, ItemData> LoadExistingItems(
        string folderPath)
    {
        var result = new Dictionary<int, ItemData>();

        string[] guids = AssetDatabase.FindAssets(
            "t:ItemData", new[] { folderPath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (data == null)
                continue;

            if (result.ContainsKey(data.itemId))
            {
                Debug.LogError(
                    $"出力先にitemId={data.itemId}の重複アセットがあります: {path}");
                throw new System.InvalidOperationException(
                    "既存アセットのIDが重複しています。");
            }

            result.Add(data.itemId, data);
        }

        return result;
    }
}

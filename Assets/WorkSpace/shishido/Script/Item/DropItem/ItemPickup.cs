using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameConst;

public class ItemPickup : MonoBehaviour, IPickupable
{

    [Header("アイテムデータ")]
    public ItemData itemData;

    public ItemState itemState;

    private float height;
    private float speed;
    private Vector3 startPosition;

    /// <summary>
    /// 生成時初期化関数
    /// </summary>
    /// <param name="type"></param>
    /// <param name="_amount"></param>
    public void Initialize(ItemData data, ItemState state)
    {
        itemData = data;
        itemState = state;

        SetAppearance();
    }

    /// <summary>
    /// 見た目のせってい
    /// </summary>
    private void SetAppearance()
    {
        if (itemData == null) return;

        //  見た目オブジェクトの生成
        Instantiate(itemData.itemPrefab, transform);
    }

    /// <summary>
    ///  見た目の更新処理
    /// </summary>
    private void AppearanceUpdate()
    {
        //  回転処理
        gameObject.transform.Rotate(new Vector3(0, ITEM_RORATE_SPEED, 0));

        //  上下運動処理
        float y = Mathf.Sin(Time.time * speed) * height;
        transform.position = startPosition + new Vector3(0f, y, 0f);
    }

    public void Pickup(PlayerManager character)
    {
        character.inventoryManager.AddItem(itemData, itemState);
        //Destroy(gameObject);
    }

    private void Start()
    {
        startPosition = transform.position;
        height = ITEM_MOVE_HEIGHT;
        speed = ITEM_MOVE_SPEED;
        SetAppearance();
    }

    private void Update()
    {
        AppearanceUpdate();
    }

}

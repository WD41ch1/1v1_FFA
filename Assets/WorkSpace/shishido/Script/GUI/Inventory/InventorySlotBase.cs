using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class InventorySlotBase<T> : MonoBehaviour,
    IDragHandler,
    IBeginDragHandler,
    IEndDragHandler,
    IInventorySlot
    where T : Enum
{
    [SerializeField]
    protected Image icon;
    [SerializeField]
    protected TextMeshProUGUI quantityText;

    protected InventoryUI inventory;
    protected GUIManager gui;
    protected T type;
    protected ItemType itemType;
    protected int quantity;

    //  ドラッグアンドドロップ関係
    protected RectTransform rectTrans;
    protected CanvasGroup canvasGroup;
    protected Transform initParent;
    protected Transform prevParent;
    protected Vector2 pointerOffset;
    protected bool isDragging = false;
    protected IDropHandler currentDropZone; // 現在ホバー中のドロップゾーン


    public abstract void Initialize(InventoryUI _inventory, T _type);

    public abstract void UpdateUI(int _quantity);

    public virtual void Droping() { }

    public virtual void DeleteData() { }

    public T GetResourceType()
    {
        return type;
    }

    public int Getquantity()
    {
        return quantity;
    }

    public abstract void Open();
    public abstract void Hide();

    #region ドラッグアンドドロップ関係

    /// <summary>
    /// ドラッグ開始時に呼ばれる
    /// </summary>
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        initParent = transform.parent;
        transform.SetParent(transform.root.root);
        //canvasGroup.blocksRaycasts = false;

        // マウス位置を親のローカル座標系に変換
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTrans.parent as RectTransform,
            eventData.position,
            null,
            out Vector2 mouseLocalPos
        );

        // ドラッグ開始時のマウス位置と UI 要素の位置の差を保存
        pointerOffset = mouseLocalPos - (Vector2)rectTrans.localPosition;
    }

    /// <summary>
    /// ドラッグ中、毎フレーム呼ばれる
    /// </summary>
    public virtual void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTrans.parent as RectTransform,
            eventData.position,
            null,
            out Vector2 mouseLocalPos
        );

        rectTrans.localPosition = mouseLocalPos - pointerOffset;

        // ドラッグ中に Raycast を発火
        DetectDropZone(eventData);
    }

    /// <summary>
    /// ドラッグ終了時に呼ばれる
    /// </summary>
    public virtual void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
       // canvasGroup.blocksRaycasts = true;

        // ドロップゾーンが見つかった場合
        if (currentDropZone != null)
        {
            Debug.Log("ドロップゾーンでドロップされました");

            // OnDrop を手動で呼び出し
            currentDropZone.OnDrop(eventData);

            currentDropZone = null;
        }
        else
        {
            Debug.Log("ドロップゾーンが見つかりません");
            transform.SetParent(initParent);
            Droping();
        }
    }

    /// <summary>
    /// Raycast でドロップ可能なオブジェクトを検出
    /// </summary>
    public virtual void DetectDropZone(PointerEventData eventData)
    {
        // UI Raycast を使って、マウス位置の UI 要素を全て取得
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        IDropHandler foundDropZone = null;

        // Raycast の結果を全て確認
        foreach (var result in results)
        {
            // IDropHandler を実装しているオブジェクトを探す
            IDropHandler dropHandler = result.gameObject.GetComponent<IDropHandler>();

            if (dropHandler != null && result.gameObject != gameObject)
            {
                foundDropZone = dropHandler;
                Debug.Log($"ドロップゾーン検出: {result.gameObject.name}");
                break;
            }
        }

        // ドロップゾーンが変わった場合
        if (foundDropZone != currentDropZone)
        {
            // 前のドロップゾーンから出た
            if (currentDropZone != null)
            {
                Debug.Log("ドロップゾーンから出ました");
                // IPointerExitHandler を呼びたい場合
                var pointerExit = currentDropZone as IPointerExitHandler;
                pointerExit?.OnPointerExit(eventData);
            }

            // 新しいドロップゾーンに入った
            if (foundDropZone != null)
            {
                Debug.Log("ドロップゾーンに入りました");
                // IPointerEnterHandler を呼びたい場合
                var pointerEnter = foundDropZone as IPointerEnterHandler;
                pointerEnter?.OnPointerEnter(eventData);
            }

            currentDropZone = foundDropZone;
        }
    }

    #endregion

    #region その他インターフェース処理

    Enum IInventorySlot.GetResourceType()
    {
        return GetResourceType();
    }

    public ItemType GetItemType()
    {
        return itemType;
    }

    #endregion

}

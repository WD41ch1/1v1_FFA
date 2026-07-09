using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class InventorySlotBase<T> : MonoBehaviour,
     IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler
    where T : Enum
{
    [SerializeField]
    private Image icon;
    [SerializeField]
    protected TextMeshProUGUI quantityText;

    protected GUIManager gui;
    protected T type;
    protected ItemType itemType;
    protected float quantity;

    //  ドラッグアンドドロップ関係
    protected RectTransform rectTrans;
    protected CanvasGroup canvasGroup;
    private bool isDragging = false;
    private Vector2 pointerOffset;


    public abstract void Initialize(GUIManager _gui, T _type);

    public abstract void UpdateUI(float _quantity);

    public T GetResourceType()
    {
        return type;
    }

    #region ドラッグアンドドロップ関係

    /// <summary>
    /// ドラッグ開始
    /// </summary>
    /// <param name="eventData"></param>
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        canvasGroup.blocksRaycasts = false;

        // ポインター位置とUI要素の位置の差を保存
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTrans.parent as RectTransform,
            eventData.position,
            null,
            out pointerOffset
        );

        pointerOffset -= (Vector2)rectTrans.localPosition;
    }

    /// <summary>
    /// ドラッグ中
    /// </summary>
    /// <param name="eventData"></param>
    public virtual void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTrans.parent as RectTransform,
            eventData.position,
            null,
            out Vector2 localPointerPosition
        );

        rectTrans.localPosition = localPointerPosition - pointerOffset;
    }

    /// <summary>
    /// ドラッグ終了
    /// </summary>
    /// <param name="eventData"></param>
    public virtual void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        canvasGroup.blocksRaycasts = true;
    }

    #endregion

}

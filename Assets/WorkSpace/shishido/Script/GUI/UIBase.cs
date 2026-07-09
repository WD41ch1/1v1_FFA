using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public abstract class UIBase : MonoBehaviour
{
    protected PlayerManager myPlayer;
    protected GUIManager gui;
    protected RectTransform rect;
    protected bool isInitialized = false;

    /// <summary>
    /// 全体の初期化
    /// </summary>
    /// <param name="_myPlayer"></param>
    /// <param name="_gui"></param>
    public void Initialize(PlayerManager _myPlayer, GUIManager _gui)
    {
        this.myPlayer = _myPlayer;
        this.gui = _gui;

        OnInitialize();
        isInitialized = true;
    }

    /// <summary>
    /// 継承先べつでの初期化
    /// </summary>
    protected abstract void OnInitialize();

    public virtual void Show() { }
    public virtual void Hide() { }
}

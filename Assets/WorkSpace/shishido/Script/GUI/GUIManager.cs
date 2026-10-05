using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GUIManager : MonoBehaviour, IGuiInitialize
{
    //  シングルトン
    //public static GUIManager instance;

    public PlayerManager myPlayer;

    [SerializeField]
    private List<UIBase> UIList;

    public CanvasGroup canvasGroup { private set; get; }

    private void Awake()
    {
        //  シングルトン
        //instance = this;
    }

    /// <summary>
    /// LocalPlayerの情報をUIに接続
    /// </summary>
    /// <param name="player"></param>
    public void RegisterPlayer(PlayerManager player)
    {
        myPlayer = player;
    }

    public void Initialize()
    {
        //  各UIの初期化
        foreach (UIBase ui in UIList)
        {
            ui.Initialize(myPlayer, this);
        }

        if (canvasGroup == null)
            canvasGroup = GetComponentInParent<CanvasGroup>();
    }

    void Update()
    {
        //  Test
        if (Input.GetKey(KeyCode.G))
        {
            //  各UIの初期化
            foreach (UIBase ui in UIList)
            {
                ui.Show();
            }
        }
        if (Input.GetKey(KeyCode.H))
        {
            //  各UIの初期化
            foreach (UIBase ui in UIList)
            {
                ui.Hide();
            }
        }
    }
}

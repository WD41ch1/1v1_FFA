using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GUIManager : MonoBehaviour
{
    public PlayerManager myPlayer;

    [SerializeField]
    private List<UIBase> UIList;

    public CanvasGroup canvasGroup { private set; get; }

    public void Initialize(PlayerManager _myPlayer)
    {
        myPlayer = _myPlayer;

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

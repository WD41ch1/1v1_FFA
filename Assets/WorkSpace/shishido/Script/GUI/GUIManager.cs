using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GUIManager : MonoBehaviour
{
    public PlayerManager myPlayer;

    [SerializeField]
    private List<UIBase> UIList;

    public void Initialize(PlayerManager _myPlayer)
    {
        myPlayer = _myPlayer;

        //  各UIの初期化
        foreach (UIBase ui in UIList)
        {
            ui.Initialize(myPlayer,this);
        }
    }

    void Update()
    {

    }
}

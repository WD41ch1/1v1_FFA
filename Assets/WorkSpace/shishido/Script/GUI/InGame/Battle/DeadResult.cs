using FishNet.Connection;
using TMPro;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;
using UnityEngine.UI;

public class DeadResult : UIBase
{
    [SerializeField] private TextMeshProUGUI KilledByPlayerText;
    [SerializeField] private Button RespawnButton;
    [SerializeField] private Button ExitButton;

    private NetworkConnection connection;

    protected override void OnInitialize()
    {
        //  イベント登録
        myPlayer.playerHealth.OnDead += ShowDeadResult;
        RespawnButton.onClick.AddListener(OnRespawnButtonClicked);
        ExitButton.onClick.AddListener(OnExitButtonClicked);

        Hidepanel();
    }

    public void ShowDeadResult(DamageInfo result,NetworkConnection con)
    {
        connection = con;
        SetKilledByPlayer(result.Attacker.name);
        Showpanel();
    }

    public void SetKilledByPlayer(string playerName)
    {
        if (KilledByPlayerText != null)
        {
            KilledByPlayerText.text = $"Killed by {playerName}";
        }
    }
    #region ボタン処理

    public void OnRespawnButtonClicked()
    {
        MatchManager.Instance?.RequestRespawn(connection);
        Destroy(this.transform.parent.gameObject);
    }

    public void OnExitButtonClicked()
    {
        //  退出処理(切断)
        ConnectionMonitoringManager
            .Instance?.OnDisConnection();
    }

    #endregion

    #region 表示切替
    public  void Showpanel()
    {
        this.gameObject.SetActive(true);
    }

    public  void Hidepanel()
    {
        this.gameObject.SetActive(false);
    }
    #endregion
}

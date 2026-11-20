using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeadResult : UIBase
{
    [SerializeField] private TextMeshProUGUI KilledByPlayerText;
    [SerializeField] private Button RespawnButton;
    [SerializeField] private Button ExitButton;

    protected override void OnInitialize()
    {
        //  イベント登録
        myPlayer.playerHealth.OnDead += ShowDeadResult;
        RespawnButton.onClick.AddListener(OnRespawnButtonClicked);
        ExitButton.onClick.AddListener(OnExitButtonClicked);

        Hide();
    }

    public void ShowDeadResult(DamageInfo result)
    {
        SetKilledByPlayer(result.Attacker.name);
        Show();
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
        MatchManager.Instance?.RequestRespawn(myPlayer);
        Hide();
    }

    public void OnExitButtonClicked()
    {
        //  退出処理(切断)
        ConnectionMonitoringManager
            .Instance?.OnDisConnection();
    }

    #endregion

    #region 表示切替
    public override void Show()
    {
        this.gameObject.SetActive(true);
    }

    public override void Hide()
    {
        this.gameObject.SetActive(false);
    }
    #endregion
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatusBerUI : UIBase
{
    [SerializeField] private Slider healthBer;
    [SerializeField] private Slider shieldBer;

    [SerializeField] private TextMeshProUGUI healthCounter;
    [SerializeField] private TextMeshProUGUI shieldCounter;

    protected override void OnInitialize()
    {
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        //  アクション登録
        myPlayer.playerHealth.OnHealthChanged += HealthUpdate;
        myPlayer.playerHealth.OnShieldChanged += ShieldUpdate;
    }


    private void HealthUpdate(float a, float b)
    {

        healthBer.value = a;
        healthCounter.text = a.ToString();
    }
    private void ShieldUpdate(float a, float b)
    {

        shieldBer.value = a;
        shieldCounter.text = a.ToString();

    }
}

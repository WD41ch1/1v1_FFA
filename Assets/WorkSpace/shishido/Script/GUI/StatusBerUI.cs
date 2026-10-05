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

        PlayerHealth health = myPlayer.playerHealth;

        //  スライダーの初期化
        SliderInitialize(health.GetMaxHealth(), health.GetMaxShield());

        //  アクション登録
        health.OnHealthChanged += HealthUpdate;
        health.OnShieldChanged += ShieldUpdate;
        Debug.Log("StatusBerUI：登録完了");
    }

    private void SliderInitialize(float _maxHealth, float _maxShield)
    {
        healthBer.maxValue = _maxHealth;
        shieldBer.maxValue = _maxShield;
    }


    private void HealthUpdate(float _maxHealth, float _currentHealth)
    {
        healthBer.value = _currentHealth;
        healthCounter.text = _currentHealth.ToString();
    }
    private void ShieldUpdate(float _maxShield, float _currentShield)
    {

        shieldBer.value = _currentShield;
        shieldCounter.text = _currentShield.ToString();

    }
}

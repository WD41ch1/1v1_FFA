using FishNet.Demo.AdditiveScenes;
using GameKit.Dependencies.Utilities;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class BOTHealthBer : MonoBehaviour, IGuiInitialize
{
    private Camera mainCamera;
    public PlayerManager owner;

    [SerializeField] private Slider healthBer;
    [SerializeField] private Slider shieldBer;

    [SerializeField] private TextMeshProUGUI healthCounter;
    [SerializeField] private TextMeshProUGUI shieldCounter;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    public void RegisterPlayer(PlayerManager _owner)
    {
        owner = _owner;
    }


    public void Initialize()
    {
        PlayerHealth health = owner.playerHealth;
        //  スライダーの初期化
        SliderInitialize(health.GetMaxHealth(), health.GetMaxShield());

        //  アクション登録
        health.OnHealthChanged += HealthUpdate;
        health.OnShieldChanged += ShieldUpdate;
    }

    private void SliderInitialize(float _maxHealth, float _maxShield)
    {
        healthBer.maxValue = _maxHealth;
        shieldBer.maxValue = _maxShield;
    }


    private void LateUpdate()
    {
        //  UIがカメラのほうに向く
        transform.LookAt(
            transform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up
        );
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

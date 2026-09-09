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
        //  アクション登録
        owner.playerHealth.OnHealthChanged += HealthUpdate;
        owner.playerHealth.OnShieldChanged += ShieldUpdate;
    }


    private void LateUpdate()
    {
        //  UIがカメラのほうに向く
        transform.LookAt(
            transform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up
        );
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

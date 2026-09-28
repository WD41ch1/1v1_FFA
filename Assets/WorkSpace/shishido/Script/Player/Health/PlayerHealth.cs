using FishNet.Demo.AdditiveScenes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    //  HP
    public float currentHealth { get; private set; }
    public float MaxHealth { get; private set; }

    //  Shield
    public float currentShield { get; private set; }
    public float MaxShield { get; private set; }

    public bool IsDead => currentHealth <= 0;

    //  UI通知アクション変数
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;

    //  自身
    public PlayerManager myPlayer { get; private set; }
    //  攻撃者
    public PlayerManager lastAttacker { get; private set; }


    /// <summary>
    /// LocalPlayerの情報を接続
    /// </summary>
    /// <param name="player"></param>
    public void RegisterPlayer(PlayerManager player)
    {
        myPlayer = player;
    }

    /// <summary>
    /// 初期化処理
    /// </summary>
    /// <param name="maxHealth">最大体力</param>
    /// <param name="maxShield">最大シールド</param>
    /// <param name="health">開始時の体力</param>
    /// <param name="shield">開始時のシールド</param>
    public void Initialize(
        float maxHealth, float maxShield,
        float health, float shield)
    {
        //  最大値の設定
        MaxHealth = maxHealth;
        MaxShield = maxShield;

        //  開始時の設定
        currentHealth = Mathf.Clamp(health, 0, MaxHealth);
        currentShield = Mathf.Clamp(shield, 0, MaxShield);

        //  UIの更新
    }

    /// <summary>
    /// UIの更新
    /// </summary>
    public void UIUpdateRequest()
    {
        OnHealthChanged.Invoke(MaxHealth, currentHealth);
        OnShieldChanged.Invoke(MaxShield, currentShield);
    }

    /// <summary>
    /// 被弾処理
    /// </summary>
    /// <param name="damageInfo"></param>
    /// <returns></returns>
    public DamageResult TakeDamage(DamageInfo damageInfo)
    {
        DamageResult result = new DamageResult();
        float remainingDamage = damageInfo.Damage;

        lastAttacker = damageInfo.Attacker;

        // Shieldダメージ
        if (currentShield > 0)
        {
            float shieldDamage =
                Mathf.Min(currentShield, remainingDamage);

            currentShield -= shieldDamage;
            remainingDamage -= shieldDamage;

            result.ShieldDamage = shieldDamage;

            //  UI通知
            OnShieldChanged.Invoke(MaxShield, currentShield);
        }


        // Healthダメージ
        if (remainingDamage > 0)
        {
            float healthDamage =
                Mathf.Min(currentHealth, remainingDamage);

            currentHealth -= healthDamage;

            result.HealthDamage = healthDamage;

            //  UI通知
            OnHealthChanged.Invoke(MaxHealth, currentHealth);
        }


        result.TotalDamage =
            result.HealthDamage +
            result.ShieldDamage;


        // 死亡判定
        if (currentHealth <= 0)
        {
            result.IsDead = true;

            Die();
        }

        return result;
    }

    /// <summary>
    /// HPの回復処理
    /// </summary>
    /// <param name="amount"></param>
    public void Heal(float amount, float limit)
    {
        // 回復
        currentHealth = Mathf.Min(currentHealth + amount, limit);
        //  UI通知
        OnHealthChanged.Invoke(MaxHealth, currentHealth);

    }

    /// <summary>
    /// シールドの回復処理
    /// </summary>
    /// <param name="amount"></param>
    public void AddShield(float amount, float limit)
    {
        // 回復
        currentShield = Mathf.Min(currentShield + amount, limit);
        //  UI通知
        OnShieldChanged.Invoke(MaxShield, currentShield);

    }

    public void SetHealth(float amount)
    {
    }

    public void SetShield(float amount)
    {
    }

    /// <summary>
    /// 死亡処理
    /// </summary>
    private void Die()
    {
        Debug.Log($"{lastAttacker?.name}に倒された");

        this.gameObject.SetActive( false );

        //Destroy(this.gameObject);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    //  HP
    public float currentHealth { get; private set; }
    public float MaxHealth { get; private set; } = 100;

    //  Shield
    public float currentShield { get; private set; }
    public float MaxShield { get; private set; } = 100;

    public bool IsDead => currentHealth <= 0;

    //  UI通知アクション変数
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;

    //  攻撃者
    public PlayerManager lastAttacker { get; private set; }

    /// <summary>
    /// 初期化処理
    /// </summary>
    /// <param name="health"></param>
    /// <param name="shield"></param>
    public void Initialize(float health, float shield)
    {
        currentHealth = Mathf.Clamp(health, 0, MaxHealth);
        currentShield = Mathf.Clamp(shield, 0, MaxShield);
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
            //OnShieldChanged.Invoke();
        }


        // Healthダメージ
        if (remainingDamage > 0)
        {
            float healthDamage =
                Mathf.Min(currentHealth, remainingDamage);

            currentHealth -= healthDamage;

            result.HealthDamage = healthDamage;

            //  UI通知
            //OnHealthChanged.Invoke();
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

    public void Heal(float amount)
    {
        currentHealth += amount;
        if (currentHealth >= MaxHealth)
            currentHealth = MaxHealth;
    }

    public void AddShield(float amount)
    {
        currentShield += amount;
        if (currentShield >= MaxShield)
            currentShield = MaxShield;
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
        Debug.Log($"{lastAttacker.name}に倒された");
    }
}

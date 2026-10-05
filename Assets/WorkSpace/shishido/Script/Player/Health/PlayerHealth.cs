using FishNet.Demo.AdditiveScenes;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    //  HP
    private readonly SyncVar<float> currentHealth = new SyncVar<float>();
    private float MaxHealth;

    //  Shield
    private readonly SyncVar<float> currentShield = new SyncVar<float>();
    private float MaxShield;

    public bool IsDead => currentHealth.Value <= 0;

    //  UI通知アクション変数
    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnShieldChanged;

    //  UI用:シールド破壊通知
    public bool haveShield;

    //  自身
    public PlayerManager myPlayer { get; private set; }
    //  攻撃者
    public PlayerManager lastAttacker { get; private set; }

    #region 初期化

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
        //  Actoinの登録
        //  ※SyncVar軽油なので、値が変わると自動で更新される
        currentHealth.OnChange += HealthChanged;
        currentShield.OnChange += ShieldChanged;

        //  最大値の設定
        MaxHealth = maxHealth;
        MaxShield = maxShield;

        //  開始時の設定
        currentHealth.Value = Mathf.Clamp(health, 0, MaxHealth);
        currentShield.Value = Mathf.Clamp(shield, 0, MaxShield);

        //  UIの更新
    }

    /// <summary>
    /// LocalPlayerの情報を接続
    /// </summary>
    /// <param name="_myPlayer"></param>
    public void RegisterPlayer(PlayerManager _myPlayer)
    {
       myPlayer = _myPlayer;
    }

    #endregion

    #region 値の増減

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
        if (currentShield.Value > 0)
        {
            float shieldDamage =
                Mathf.Min(currentShield.Value, remainingDamage);

            currentShield.Value -= shieldDamage;
            remainingDamage -= shieldDamage;

            result.ShieldDamage = shieldDamage;

        }


        // Healthダメージ
        if (remainingDamage > 0)
        {
            float healthDamage =
                Mathf.Min(currentHealth.Value, remainingDamage);

            currentHealth.Value -= healthDamage;

            result.HealthDamage = healthDamage;
        }


        result.TotalDamage =
            result.HealthDamage +
            result.ShieldDamage;


        // 死亡判定
        if (currentHealth.Value <= 0)
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
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, limit);

    }

    /// <summary>
    /// シールドの回復処理
    /// </summary>
    /// <param name="amount"></param>
    public void AddShield(float amount, float limit)
    {
        //  シールド所持フラグをあげる
        if (currentShield.Value <= 0 && !haveShield) haveShield = true;
        // 回復
        currentShield.Value = Mathf.Min(currentShield.Value + amount, limit);

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

        this.gameObject.SetActive(false);

        //Destroy(this.gameObject);
    }

    #endregion

    /// <summary>
    /// UIの更新
    /// </summary>
    public void UIUpdateRequest()
    {
        if (myPlayer == null || !myPlayer.IsInitialized) return;

        float health = currentHealth.Value;
        float shield = currentShield.Value;

        OnHealthChanged?.Invoke(MaxHealth, health);
        OnShieldChanged?.Invoke(MaxShield, shield);
    }

    #region Action

    /// <summary>
    /// Health更新通知
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="next"></param>
    /// <param name="asServer"></param>
    private void HealthChanged(
    float previous,
    float next,
    bool asServer)
    {
        if (!IsOwner ||
            myPlayer == null ||
            !myPlayer.IsInitialized)
            return;

        // 自分のHP UIだけ更新
        OnHealthChanged?.Invoke(MaxHealth, next);
        //guiManager.UpdateHealth(next);
    }

    /// <summary>
    /// シールド更新通知
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="next"></param>
    /// <param name="asServer"></param>
    private void ShieldChanged(
    float previous,
    float next,
    bool asServer)
    {
        if (!IsOwner ||
            myPlayer == null ||
            !myPlayer.IsInitialized)
            return;

        // 自分のHP UIだけ更新
        OnShieldChanged?.Invoke(MaxShield, next);
        //guiManager.UpdateHealth(next);
    }

    #endregion

    #region Getter

    public float GetcurrentHealth() { return currentHealth.Value; }
    public float GetcurrentShield() { return currentShield.Value; }

    public float GetMaxHealth() { return MaxHealth; }
    public float GetMaxShield() { return MaxShield; }

    #endregion
}

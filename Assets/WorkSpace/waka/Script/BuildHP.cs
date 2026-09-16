using UnityEngine;

[DisallowMultipleComponent]
public class BuildHP : MonoBehaviour
{
    [Header("最大HP")]
    [SerializeField, Min(1)] private int maxHP = 100;

    [Header("現在のHP（再生中の確認用）")]
    [SerializeField] private int currentHP;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;

    private bool isBroken;

    private void Awake()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = maxHP;
    }

    // 攻撃を受けたときに呼ぶ
    public void TakeDamage(int damage)
    {
        if (isBroken || damage <= 0) return;

        currentHP = Mathf.Max(0, currentHP - damage);

        if (currentHP == 0)
        {
            BreakBuilding();
        }
    }

    // 修理するときに呼ぶ
    public void Repair(int amount)
    {
        if (isBroken || amount <= 0) return;

        currentHP += Mathf.Min(amount, maxHP - currentHP);
    }

    private void BreakBuilding()
    {
        isBroken = true;

        // 当たり判定をすぐに消し、編集画面も終了させる
        gameObject.SetActive(false);

        // 子にある編集後の形状もまとめて削除
        Destroy(gameObject);
    }
}
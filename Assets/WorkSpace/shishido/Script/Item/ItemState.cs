
using UnityEngine;

/// <summary>
/// アイテムの状態を管理しているクラス
/// [武器]　→　マガジンに入っている残弾数
/// [回復]  →　現在持っているスタック数
/// [投げもの]  →　なし (おそらくスタック数になる予定)
/// </summary>
public class ItemState
{
    //  使用しているスロット番号
    public int useSlotNum;

    public int currentAmmo;

    public int currentStack;


    public ItemState(BerItemType itemType, int amount, int slotNUm)
    {
        switch (itemType)
        {
            case BerItemType.Weapon:
                currentAmmo = amount;
                break;
            case BerItemType.Healing:
                currentStack = amount;
                break;
            case BerItemType.Throwing:
                break;
        }

        useSlotNum = slotNUm;

    }

    /// <summary>
    /// useSlotNumの外部セッター
    /// </summary>
    /// <param name="num"></param>
    public void SetuseSlotNum(int num)
    {
        int prevNum = useSlotNum;

        useSlotNum = num;

        Debug.Log($"変更前:{prevNum},変更後:{useSlotNum}");
    }
}
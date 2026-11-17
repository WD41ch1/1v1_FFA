using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DoOnce
{
    // 実行済みのキーを管理
    private static readonly HashSet<object> _executedKeys = new();

    /// <summary>
    /// 一度だけ処理を実行する
    /// </summary>
    /// <returns>今回実行できた場合 true</returns>
    public static bool TryExecute(object key)
    {
        if (key == null)
            return false;

        return _executedKeys.Add(key);
    }

    /// <summary>
    /// 実行制限を解除する
    /// </summary>
    public static void Reset(object key)
    {
        if (key == null)
            return;

        _executedKeys.Remove(key);
    }

    /// <summary>
    /// すべての実行制限を解除する
    /// </summary>
    public static void ResetAll()
    {
        _executedKeys.Clear();
    }
}
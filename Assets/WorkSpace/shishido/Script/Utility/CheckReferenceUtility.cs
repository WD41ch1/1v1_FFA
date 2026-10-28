using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

public static class CheckReferenceUtility
{

    #region checker

    /// <summary>
    /// 汎用オブジェクトNULLチェックmethod
    /// </summary>
    /// <param name="target"></param>
    /// <param name="objectName"></param>
    /// <returns></returns>
    public static bool CheckReference(UnityEngine.Object target, string objectName, bool _isShowLog = true, UnityEngine.Object context = null)
    {
        // 通常のnullチェック
        if (target == null)
        {
            GameDebug.Error($"{objectName} が設定されていません。", _isShowLog, context);
            return false;
        }

        // Unityオブジェクトの場合は破棄済みかも確認
        if (target is UnityEngine.Object unityObject && unityObject == null)
        {
            GameDebug.Error($"{objectName} は破棄されています。", _isShowLog, context);
            return false;
        }

        return true;
    }


    #endregion

    #region UniTask

    /// <summary>
    /// 参照が取得できるまで待機する
    /// </summary>
    /// <typeparam name="T">参照の型</typeparam>
    /// <param name="getReference">参照を取得する処理</param>
    /// <param name="objectName">ログに表示する名前</param>
    /// <param name="timeoutSeconds">タイムアウトまでの秒数</param>
    /// <param name="destroyToken">オブジェクト破棄時のキャンセルトークン</param>
    /// <returns>取得成功ならtrue、失敗・キャンセルならfalse</returns>
    public static async UniTask<bool> WaitForReference<T>(
        Func<T> getReference,
        string objectName,
        float timeoutSeconds,
        CancellationToken destroyToken)
        where T : UnityEngine.Object
    {
        if (getReference == null)
        {
            GameDebug.Error("参照取得処理が設定されていません。");
            return false;
        }

        if (timeoutSeconds <= 0f)
        {
            GameDebug.Error("タイムアウト時間は0より大きくしてください。");
            return false;
        }

        // すでに取得できている場合は待機しない
        if (getReference() != null)
            return true;

        // タイムアウト用トークン
        using var timeoutCts =
            new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        // 破棄とタイムアウトの両方でキャンセル
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                destroyToken,
                timeoutCts.Token);

        try
        {
            await UniTask.WaitUntil(
                () => getReference() != null,
                cancellationToken: linkedCts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            // タイムアウトした場合
            if (timeoutCts.IsCancellationRequested &&
                !destroyToken.IsCancellationRequested)
            {
                GameDebug.Error(
                    $"{objectName} を{timeoutSeconds}秒以内に取得できませんでした。");
            }

            return false;
        }
    }

    /// <summary>
    /// Bool条件が成立するまで待機する
    /// </summary>
    /// <param name="getCondition">判定する条件</param>
    /// <param name="conditionName">ログに表示する名前</param>
    /// <param name="timeoutSeconds">タイムアウトまでの秒数</param>
    /// <param name="destroyToken">オブジェクト破棄時のキャンセルトークン</param>
    /// <returns>条件成立ならtrue、失敗・キャンセルならfalse</returns>
    public static async UniTask<bool> WaitForReference(
        Func<bool> getCondition,
        string conditionName,
        float timeoutSeconds,
        CancellationToken destroyToken)
    {
        if (getCondition == null)
        {
            GameDebug.Error("判定処理が設定されていません。");
            return false;
        }

        if (timeoutSeconds <= 0f)
        {
            GameDebug.Error("タイムアウト時間は0より大きくしてください。");
            return false;
        }

        // すでに条件を満たしている場合は待機しない
        if (getCondition())
            return true;

        using var timeoutCts =
            new CancellationTokenSource(
            TimeSpan.FromSeconds(timeoutSeconds));

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                destroyToken,
                timeoutCts.Token);

        try
        {
            await UniTask.WaitUntil(
                getCondition,
                cancellationToken: linkedCts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            if (timeoutCts.IsCancellationRequested &&
                !destroyToken.IsCancellationRequested)
            {
                GameDebug.Error(
                    $"{conditionName} が{timeoutSeconds}秒以内に成立しませんでした。");
            }

            return false;
        }
    }

    #endregion

}

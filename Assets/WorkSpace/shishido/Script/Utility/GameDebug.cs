using UnityEngine;

public static class GameDebug
{
    // 通常ログ：白
    public static void Log(string message,bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=white>[LOG]</color> {message}", context);
    }

    // 成功：緑
    public static void Success(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=lime>[SUCCESS]</color> {message}", context);
    }

    // 情報：水色
    public static void Info(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=cyan>[INFO]</color> {message}", context);
    }

    // 注意：黄色
    public static void Warning(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.LogWarning($"<color=yellow>[WARNING]</color> {message}", context);
    }

    // エラー：赤
    public static void Error(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.LogError($"<color=red>[ERROR]</color> {message}", context);
    }


    // 初期化処理：青
    public static void Initialize(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=blue>[INIT]</color> {message}", context);
    }

    // 入力処理：緑
    public static void Input(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=green>[INPUT]</color> {message}", context);
    }

    // UI処理：ピンク
    public static void UI(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=#FF69B4>[UI]</color> {message}", context);
    }

    // 処理開始：太字
    public static void StartLog(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<b><color=yellow>[START]</color> {message}</b>", context);
    }

    // 処理終了：太字
    public static void EndLog(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<b><color=lime>[END]</color> {message}</b>", context);
    }

    // 詳細ログ：灰色
    public static void Detail(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=grey>[DETAIL]</color> {message}", context);
    }

    #region FishNet用

    // サーバー処理：オレンジ
    public static void Server(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=orange>[SERVER]</color> {message}", context);
    }
    public static void ServerWarning(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.LogWarning($"<color=orange>[SERVER]</color><color=yellow>[WARNING]</color> {message}", context);
    }

    // クライアント処理：水色
    public static void Client(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=cyan>[CLIENT]</color> {message}", context);
    }
    public static void ClientWarning(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.LogWarning($"<color=cyan>[CLIENT]</color><color=yellow>[WARNING]</color> {message}", context);
    }

    // ネットワーク同期：紫
    public static void Sync(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.Log($"<color=magenta>[SYNC]</color> {message}", context);
    }
    public static void SyncWarning(string message, bool _activ = true, Object context = null)
    {
        if (!_activ) return;

        Debug.LogWarning($"<color=magenta>[SYNC]</color><color=yellow>[WARNING]</color> {message}", context);
    }

    #endregion
}

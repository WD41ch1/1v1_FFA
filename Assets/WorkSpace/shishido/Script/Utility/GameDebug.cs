using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameDebug
{
    // 通常ログ：白
    public static void Log(string message)
    {
        Debug.Log($"<color=white>[LOG]</color> {message}");
    }

    // 成功：緑
    public static void Success(string message)
    {
        Debug.Log($"<color=lime>[SUCCESS]</color> {message}");
    }

    // 情報：水色
    public static void Info(string message)
    {
        Debug.Log($"<color=cyan>[INFO]</color> {message}");
    }

    // 注意：黄色
    public static void Warning(string message)
    {
        Debug.LogWarning($"<color=yellow>[WARNING]</color> {message}");
    }

    // エラー：赤
    public static void Error(string message)
    {
        Debug.LogError($"<color=red>[ERROR]</color> {message}");
    }


    // 初期化処理：青
    public static void Initialize(string message)
    {
        Debug.Log($"<color=blue>[INIT]</color> {message}");
    }

    // 入力処理：緑
    public static void Input(string message)
    {
        Debug.Log($"<color=green>[INPUT]</color> {message}");
    }

    // UI処理：ピンク
    public static void UI(string message)
    {
        Debug.Log($"<color=#FF69B4>[UI]</color> {message}");
    }

    // 処理開始：太字
    public static void StartLog(string message)
    {
        Debug.Log($"<b><color=yellow>[START]</color> {message}</b>");
    }

    // 処理終了：太字
    public static void EndLog(string message)
    {
        Debug.Log($"<b><color=lime>[END]</color> {message}</b>");
    }

    // 詳細ログ：灰色
    public static void Detail(string message)
    {
        Debug.Log($"<color=grey>[DETAIL]</color> {message}");
    }

    #region FishNet用

    // サーバー処理：オレンジ
    public static void Server(string message)
    {
        Debug.Log($"<color=orange>[SERVER]</color> {message}");
    }

    // クライアント処理：水色
    public static void Client(string message)
    {
        Debug.Log($"<color=cyan>[CLIENT]</color> {message}");
    }

    // ネットワーク同期：紫
    public static void Sync(string message)
    {
        Debug.Log($"<color=magenta>[SYNC]</color> {message}");
    }

    #endregion

}

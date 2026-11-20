using System;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class NetworkSoundEmitter : NetworkBehaviour
{
    [Header("クライアントが指定できる音源位置の範囲")]
    [SerializeField, Min(1f)]
    private float maxRequestDistance = 12f;

    [Header("同じ音の通知間隔の最低値")]
    [SerializeField, Min(0.01f)]
    private float minimumInterval = 0.025f;

    [Header("走行中だけ再生する足音")]
    [SerializeField]
    private AudioClip runningClip;

    [SerializeField, Range(0f, 1f)]
    private float runningVolume = 0.55f;

    [SerializeField, Range(0.5f, 2f)]
    private float runningPitch = 1f;

    [SerializeField, Min(0.1f)]
    private float runningMinDistance = 1.5f;

    [SerializeField, Min(0.2f)]
    private float runningMaxDistance = 20f;

    private readonly Dictionary<GameSoundId, float> lastPlayed =
        new Dictionary<GameSoundId, float>();

    private AudioSource runningSource;
    private bool warnedMissingManager;

    // 自分の操作による走行状態。
    private bool localRunning;

    // サーバーが保持する走行状態。
    private bool serverRunning;

    // 相手のPCで再生する走行状態。
    private bool remoteRunning;

    private float nextRunningRefresh;
    private float lastServerRunningRefresh;

    // 走っている間だけ定期的に状態を更新する。
    private const float RunningRefreshInterval = 0.25f;

    // 操作側の更新が途絶えたときに鳴り続けるのを防ぐ。
    private const float RunningTimeout = 1f;

    private void Awake()
    {
        // 足音専用。建築音や銃声とは別に停止できる。
        GameObject soundObject = new GameObject("RunningFootstep");
        soundObject.transform.SetParent(transform, false);

        runningSource = soundObject.AddComponent<AudioSource>();

        runningSource.playOnAwake = false;
        runningSource.loop = true;
        runningSource.spatialBlend = 1f;
        runningSource.rolloffMode = AudioRolloffMode.Linear;
        runningSource.dopplerLevel = 0f;

        runningSource.clip = runningClip;
        runningSource.volume = runningVolume;
        runningSource.pitch = runningPitch;
        runningSource.minDistance =
            Mathf.Max(0.1f, runningMinDistance);
        runningSource.maxDistance =
            Mathf.Max(
                runningSource.minDistance + 0.1f,
                runningMaxDistance);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        lastPlayed.Clear();
        serverRunning = false;
        lastServerRunningRefresh = Time.unscaledTime;
    }

    private void Update()
    {
        if (IsServerInitialized &&
            serverRunning &&
            Time.unscaledTime - lastServerRunningRefresh > RunningTimeout)
        {
            SetServerRunning(false);
        }

        if (!IsClientInitialized)
        {
            ApplyRunningPlayback(false);
            return;
        }

        ApplyRunningPlayback(
            isActiveAndEnabled &&
            (IsOwner ? localRunning : remoteRunning));
    }

    /// <summary>
    /// 所有者のPlayerMovementから毎FixedUpdate呼ぶ。
    /// 自分は即座に再生・停止し、相手にはサーバー経由で通知する。
    /// </summary>
    public void SetRunning(bool running)
    {
        if (!IsClientInitialized || !IsOwner)
            return;

        bool changed = localRunning != running;
        localRunning = running;

        ApplyRunningPlayback(running);

        // 停止中は、状態が変わったときだけ通知する。
        // 走行中は、停止通知がなくなった場合に備えて定期更新する。
        if (!changed &&
            (!running || Time.unscaledTime < nextRunningRefresh))
        {
            return;
        }

        nextRunningRefresh =
            Time.unscaledTime + RunningRefreshInterval;

        SetRunningServerRpc(running);
    }

    [ServerRpc]
    private void SetRunningServerRpc(bool running)
    {
        lastServerRunningRefresh = Time.unscaledTime;
        SetServerRunning(running);
    }

    private void SetServerRunning(bool running)
    {
        if (serverRunning == running)
            return;

        serverRunning = running;
        SetRunningObserversRpc(running);
    }

    // 途中からこのPlayerが見えるようになった相手にも、
    // 現在の走行状態を渡す。
    [ObserversRpc(BufferLast = true)]
    private void SetRunningObserversRpc(bool running)
    {
        remoteRunning = running;

        // 所有者は自分の入力を即座に反映済み。
        // 遅れて届いた通知で足音が再開しないようにする。
        if (!IsOwner && IsClientInitialized)
        {
            ApplyRunningPlayback(
                isActiveAndEnabled && running);
        }
    }

    private void ApplyRunningPlayback(bool running)
    {
        if (runningSource == null)
            return;

        if (!running || runningSource.clip == null)
        {
            if (runningSource.isPlaying)
                runningSource.Stop();

            return;
        }

        // 再生中なら最初から再生し直さない。
        if (!runningSource.isPlaying)
            runningSource.Play();
    }

    public override void OnStopClient()
    {
        localRunning = false;
        remoteRunning = false;
        ApplyRunningPlayback(false);

        base.OnStopClient();
    }

    private void OnDisable()
    {
        if (IsClientInitialized && IsOwner)
            SetRunning(false);

        localRunning = false;
        remoteRunning = false;
        ApplyRunningPlayback(false);
    }

    /// <summary>
    /// 単発の効果音を所有者クライアントから要求する。
    /// 建築音・射撃音などに使用する。
    /// </summary>
    public void RequestSound(GameSoundId id, Vector3 position)
    {
        if (!IsClientInitialized || !IsOwner)
            return;

        RequestSoundServerRpc(id, position);
    }

    [ServerRpc]
    private void RequestSoundServerRpc(
        GameSoundId id,
        Vector3 position)
    {
        if (!ValidPosition(position))
            return;

        float allowedDistance =
            Mathf.Max(1f, maxRequestDistance);

        if ((position - transform.position).sqrMagnitude >
            allowedDistance * allowedDistance)
        {
            return;
        }

        PlayOnServer(id, position);
    }

    /// <summary>
    /// サーバーで確定した建築・編集・射撃などから呼ぶ。
    /// </summary>
    public void PlayOnServer(GameSoundId id, Vector3 position)
    {
        if (!IsServerInitialized || !IsSpawned)
            return;

        if (!Enum.IsDefined(typeof(GameSoundId), id) ||
            !ValidPosition(position))
        {
            return;
        }

        float now = Time.unscaledTime;

        if (lastPlayed.TryGetValue(id, out float previous) &&
            now - previous < minimumInterval)
        {
            return;
        }

        lastPlayed[id] = now;

        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        PlayObserversRpc(id, position, seed);
    }

    [ObserversRpc]
    private void PlayObserversRpc(
        GameSoundId id,
        Vector3 position,
        int seed)
    {
        if (!IsClientInitialized)
            return;

        if (AudioManager.Instance == null)
        {
            if (!warnedMissingManager)
            {
                warnedMissingManager = true;

                Debug.LogWarning(
                    "NetworkSoundEmitter：シーンにAudioManagerがありません。",
                    this);
            }

            return;
        }

        AudioManager.Instance.PlaySE(id, position, seed);
    }

    private static bool ValidPosition(Vector3 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y) &&
            !float.IsInfinity(value.z);
    }
}
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

    private bool localRunning;
    private bool serverRunning;
    private bool remoteRunning;

    private float nextRunningRefresh;
    private float lastServerRunningRefresh;

    private const float RunningRefreshInterval = 0.25f;
    private const float RunningTimeout = 1f;

    private void Awake()
    {
        // 足音専用。銃声とは別に停止できる。
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

        runningSource.maxDistance = Mathf.Max(
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
    /// 所有者のPlayerMovementから呼ぶ。
    /// </summary>
    public void SetRunning(bool running)
    {
        if (!IsClientInitialized || !IsOwner)
            return;

        bool changed = localRunning != running;
        localRunning = running;

        ApplyRunningPlayback(running);

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

    [ObserversRpc(BufferLast = true)]
    private void SetRunningObserversRpc(bool running)
    {
        remoteRunning = running;

        // 所有者は自身の入力で即座に反映する。
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

        if (!runningSource.isPlaying)
            runningSource.Play();
    }

    /// <summary>
    /// 所有者クライアントから効果音を要求する。
    /// 発射音は自分のPCで即座に再生する。
    /// </summary>
    public void RequestSound(GameSoundId id, Vector3 position)
    {
        if (!IsClientInitialized || !IsOwner)
            return;

        if (!Enum.IsDefined(typeof(GameSoundId), id) ||
            !ValidPosition(position))
        {
            return;
        }

        float allowedDistance = Mathf.Max(1f, maxRequestDistance);

        if ((position - transform.position).sqrMagnitude >
            allowedDistance * allowedDistance)
        {
            return;
        }

        int seed = UnityEngine.Random.Range(0, int.MaxValue);
        bool predictedGunfire = AudioManager.IsGunfire(id);

        // ボタンを離した後に、遅れて届いた発射通知で
        // 自分の音が再開することを防ぐ。
        if (predictedGunfire)
            PlayLocalSound(id, position, seed);

        RequestSoundServerRpc(
            id,
            position,
            seed);
    }

    [ServerRpc]
    private void RequestSoundServerRpc(
        GameSoundId id,
        Vector3 position,
        int seed)
    {
        if (!Enum.IsDefined(typeof(GameSoundId), id) ||
            !ValidPosition(position))
        {
            return;
        }

        float allowedDistance = Mathf.Max(1f, maxRequestDistance);

        if ((position - transform.position).sqrMagnitude >
            allowedDistance * allowedDistance)
        {
            return;
        }

        if (!CanPlayOnServer(id))
            return;

        // 発射音のみ所有者側で再生済み。
        PlayObserversRpc(
            id,
            position,
            seed,
            AudioManager.IsGunfire(id));
    }

    /// <summary>
    /// サーバーで確定した建築・編集などから呼ぶ。
    /// RequestSoundと同じイベントに重複して呼ばない。
    /// </summary>
    public void PlayOnServer(GameSoundId id, Vector3 position)
    {
        if (!Enum.IsDefined(typeof(GameSoundId), id) ||
            !ValidPosition(position))
        {
            return;
        }

        if (!CanPlayOnServer(id))
            return;

        int seed = UnityEngine.Random.Range(0, int.MaxValue);

        // サーバー直接呼び出しは所有者も含めて再生する。
        PlayObserversRpc(id, position, seed, false);
    }

    private bool CanPlayOnServer(GameSoundId id)
    {
        if (!IsServerInitialized || !IsSpawned)
            return false;

        float now = Time.unscaledTime;

        if (lastPlayed.TryGetValue(id, out float previous) &&
            now - previous < minimumInterval)
        {
            return false;
        }

        lastPlayed[id] = now;
        return true;
    }

    [ObserversRpc]
    private void PlayObserversRpc(
        GameSoundId id,
        Vector3 position,
        int seed,
        bool alreadyPlayedByOwner)
    {
        if (!IsClientInitialized || !isActiveAndEnabled)
            return;

        // 所有者はRequestSoundで再生済み。
        if (alreadyPlayedByOwner && IsOwner)
            return;

        PlayLocalSound(id, position, seed);
    }

    private void PlayLocalSound(
        GameSoundId id,
        Vector3 position,
        int seed)
    {
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

        // このEmitterの音として登録する。
        AudioManager.Instance.PlaySE(
            id,
            position,
            seed,
            this);
    }

    /// <summary>
    /// 所有者が射撃を止めたときに呼ぶ。
    /// このプレイヤーの発射音だけ停止する。
    /// </summary>
    public void StopGunfire()
    {
        if (!IsClientInitialized || !IsOwner)
            return;

        // 自分は通信を待たずに停止。
        StopLocalGunfire();

        StopGunfireServerRpc();
    }

    private void StopLocalGunfire()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopGunfire(this);
    }

    [ServerRpc]
    private void StopGunfireServerRpc()
    {
        StopGunfireObserversRpc();
    }

    [ObserversRpc]
    private void StopGunfireObserversRpc()
    {
        if (!IsClientInitialized)
            return;

        // 自分は停止済み。
        // 遅れて届いた停止通知で次の射撃音を切らない。
        if (IsOwner)
            return;

        StopLocalGunfire();
    }

    public override void OnStopClient()
    {
        StopLocalGunfire();

        localRunning = false;
        remoteRunning = false;
        ApplyRunningPlayback(false);

        base.OnStopClient();
    }

    private void OnDisable()
    {
        if (IsClientInitialized && IsOwner)
        {
            SetRunning(false);
            StopGunfire();
        }

        StopLocalGunfire();

        localRunning = false;
        remoteRunning = false;
        ApplyRunningPlayback(false);
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
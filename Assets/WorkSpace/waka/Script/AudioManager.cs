using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum GameSoundId
{
    FootstepGround = 0,
    FootstepWood = 1,
    Jump = 2,
    Land = 3,

    AssaultFire = 10,
    PistolFire = 11,
    Reload = 12,

    BuildWood = 20,
    BuildBreak = 21,
    BuildEdit = 22
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class SoundSetting
    {
        public GameSoundId id;

        [Tooltip("複数登録するとランダムに選ぶ")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 0.7f;

        [Header("音程のランダム範囲")]
        [Range(0.5f, 2f)]
        public float minPitch = 0.95f;

        [Range(0.5f, 2f)]
        public float maxPitch = 1.05f;

        [Header("3Dサウンドの距離")]
        [Min(0.1f)]
        public float minDistance = 2f;

        [Min(0.2f)]
        public float maxDistance = 30f;
    }

    [Header("効果音の登録")]
    [SerializeField]
    private List<SoundSetting> sounds = new List<SoundSetting>();

    [Header("同時再生数")]
    [SerializeField, Min(1)]
    private int voiceCount = 32;

    [Header("全体の効果音量")]
    [SerializeField, Range(0f, 1f)]
    private float masterVolume = 1f;

    [Header("任意：AudioMixerの出力先")]
    [SerializeField]
    private AudioMixerGroup outputMixer;

    private readonly Dictionary<GameSoundId, SoundSetting> lookup =
        new Dictionary<GameSoundId, SoundSetting>();

    private readonly HashSet<GameSoundId> warnedSounds =
        new HashSet<GameSoundId>();

    private AudioSource[] voices;
    private float[] voiceVolumes;
    private int nextVoice;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 専用の空オブジェクトに付ける。
        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject);

        foreach (SoundSetting sound in sounds)
        {
            if (sound == null)
                continue;

            if (lookup.ContainsKey(sound.id))
            {
                Debug.LogWarning(
                    $"AudioManager：{sound.id}が重複しています。",
                    this);
                continue;
            }

            lookup.Add(sound.id, sound);
        }

        int count = Mathf.Max(1, voiceCount);

        voices = new AudioSource[count];
        voiceVolumes = new float[count];

        for (int i = 0; i < count; i++)
        {
            GameObject voiceObject =
                new GameObject("SE_" + i);

            voiceObject.transform.SetParent(transform, false);

            AudioSource source =
                voiceObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;

            // 位置・距離に応じて聞こえる3Dサウンド。
            source.spatialBlend = 1f;

            // 最大距離で音量が0になる。
            source.rolloffMode = AudioRolloffMode.Linear;

            // 高速移動で音程が変化するのを防ぐ。
            source.dopplerLevel = 0f;

            source.outputAudioMixerGroup = outputMixer;

            voices[i] = source;
        }
    }

    /// <summary>
    /// このPCだけで音を再生する。
    /// オンライン通知はNetworkSoundEmitterが担当する。
    /// </summary>
    public void PlaySE(
        GameSoundId id,
        Vector3 position,
        int variationSeed)
    {
        if (!lookup.TryGetValue(id, out SoundSetting setting) ||
            setting.clips == null ||
            setting.clips.Length == 0)
        {
            WarnMissing(id);
            return;
        }

        // サーバーから同じSeedを受け取り、
        // 各PCで同じ音と音程を選ぶ。
        System.Random random = new System.Random(variationSeed);

        int startIndex = random.Next(setting.clips.Length);
        AudioClip clip = null;

        for (int i = 0; i < setting.clips.Length; i++)
        {
            int index = (startIndex + i) % setting.clips.Length;

            if (setting.clips[index] != null)
            {
                clip = setting.clips[index];
                break;
            }
        }

        if (clip == null)
        {
            WarnMissing(id);
            return;
        }

        int voiceIndex = FindVoice();
        AudioSource source = voices[voiceIndex];

        source.Stop();
        source.transform.position = position;
        source.clip = clip;

        float low = Mathf.Clamp(
            Mathf.Min(setting.minPitch, setting.maxPitch),
            0.5f,
            2f);

        float high = Mathf.Clamp(
            Mathf.Max(setting.minPitch, setting.maxPitch),
            low,
            2f);

        source.pitch = Mathf.Lerp(
            low,
            high,
            (float)random.NextDouble());

        voiceVolumes[voiceIndex] = Mathf.Clamp01(setting.volume);
        source.volume = voiceVolumes[voiceIndex] * masterVolume;

        source.minDistance = Mathf.Max(0.1f, setting.minDistance);
        source.maxDistance = Mathf.Max(
            source.minDistance + 0.1f,
            setting.maxDistance);

        source.Play();
    }

    private int FindVoice()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].isPlaying)
                return i;
        }

        // 上限に達したら順番に再利用する。
        int result = nextVoice;
        nextVoice = (nextVoice + 1) % voices.Length;
        return result;
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);

        if (voices == null)
            return;

        for (int i = 0; i < voices.Length; i++)
        {
            voices[i].volume =
                voiceVolumes[i] * masterVolume;
        }
    }

    private void WarnMissing(GameSoundId id)
    {
        if (!warnedSounds.Add(id))
            return;

        Debug.LogWarning(
            $"AudioManager：{id}のAudioClipを登録してください。",
            this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
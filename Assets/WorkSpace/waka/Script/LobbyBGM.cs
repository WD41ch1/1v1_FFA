using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class LobbyBGM : MonoBehaviour
{
    private static LobbyBGM instance;

    private AudioSource audioSource;
    private bool playbackStarted;
    private bool pausedForBattle;
    private bool sceneCheckRequired;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // 遷移元から流れているBGMを優先する。
        if (instance != null && instance != this)
        {
            audioSource.Stop();
            Destroy(gameObject);
            return;
        }

        instance = this;

        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        sceneCheckRequired = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneCheckRequired = true;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        sceneCheckRequired = true;
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        sceneCheckRequired = true;
    }

    private void LateUpdate()
    {
        if (instance != this || !sceneCheckRequired)
            return;

        sceneCheckRequired = false;

        UpdatePlayback();
    }

    private bool IsBattleLoaded()
    {
        // 追加読み込みでも対戦シーンを検出する。
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
                continue;

            if (scene.name == "BattleScene" ||
                scene.name == "BattleScene_β")
            {
                return true;
            }
        }

        return false;
    }

    private void UpdatePlayback()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning(
                "LobbyBGM：AudioClipにBGMを登録してください。",
                this);
            return;
        }

        if (IsBattleLoaded())
        {
            if (playbackStarted && !pausedForBattle)
            {
                // 再生位置を保持して一時停止する。
                audioSource.Pause();
                pausedForBattle = true;
            }

            return;
        }

        if (!playbackStarted)
        {
            audioSource.Play();
            playbackStarted = true;
            pausedForBattle = false;
            return;
        }

        if (pausedForBattle)
        {
            // 対戦から戻った場合も続きから再開する。
            audioSource.UnPause();
            pausedForBattle = false;
        }

        // Title・接続画面・ロビー間では
        // PlayやStopを呼ばず、そのまま流し続ける。
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        instance = null;
    }
}
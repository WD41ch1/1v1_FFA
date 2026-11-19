using UnityEngine;
using UnityEngine.InputSystem;

public static class KeybindSession
{
    private static string overridesJson;
    private static float resumeTime;

    public static int Revision { get; private set; }
    public static bool SettingsOpen { get; private set; }

    public static bool BlocksGameplay
    {
        get
        {
            if (SettingsOpen)
                return true;

            if (Time.unscaledTime < resumeTime)
                return true;

            if (resumeTime > 0f &&
                Mouse.current != null &&
                (Mouse.current.leftButton.isPressed ||
                 Mouse.current.rightButton.isPressed))
            {
                return true;
            }

            resumeTime = 0f;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnStartup()
    {
        overridesJson = null;
        resumeTime = 0f;
        Revision = 0;
        SettingsOpen = false;
    }

    public static void SetSettingsOpen(bool open)
    {
        SettingsOpen = open;

        if (!open)
            resumeTime = Time.unscaledTime + 0.2f;
    }

    public static void Store(InputActionAsset actions)
    {
        if (actions == null)
            return;

        // 起動中のメモリだけに保持する。
        // ファイルやPlayerPrefsには保存しない。
        overridesJson = actions.SaveBindingOverridesAsJson();
        Revision++;
    }

    public static void Apply(InputActionAsset actions)
    {
        if (actions == null)
            return;

        actions.RemoveAllBindingOverrides();

        if (!string.IsNullOrEmpty(overridesJson))
        {
            actions.LoadBindingOverridesFromJson(overridesJson);
        }
    }
}
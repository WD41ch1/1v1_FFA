using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PlayerInputController))]
public class PlayerKeybindReceiver : MonoBehaviour
{
    private PlayerInputController controller;
    private InputActionAsset lastActions;

    private int appliedRevision = -1;

    // このスクリプトが入力を止めた場合だけ再開する。
    private bool pausedBySettings;

    private void Awake()
    {
        controller = GetComponent<PlayerInputController>();
    }

    private void Update()
    {
        if (controller == null)
            return;

        // 他人のPlayerには適用しない。
        // 設定画面のために停止した自分のPlayerは処理を続ける。
        if (!pausedBySettings && !controller.CanProcessInput)
            return;

        var playerInput = controller.playerInput;

        if (playerInput == null || playerInput.actions == null)
            return;

        InputActionAsset actions = playerInput.actions;

        // Player生成時やキー配置変更時に反映する。
        if (lastActions != actions ||
            appliedRevision != KeybindSession.Revision)
        {
            KeybindSession.Apply(actions);

            lastActions = actions;
            appliedRevision = KeybindSession.Revision;
        }

        if (KeybindSession.BlocksGameplay)
        {
            if (!pausedBySettings && controller.enabled)
            {
                pausedBySettings = true;

                // PlayerInputControllerのOnDisableで
                // 移動入力・射撃・ActionMapを停止する。
                controller.enabled = false;
            }
        }
        else
        {
            ResumeInput();
        }
    }

    private void ResumeInput()
    {
        if (!pausedBySettings)
            return;

        pausedBySettings = false;

        if (controller != null)
            controller.enabled = true;
    }

    private void OnDisable()
    {
        ResumeInput();
    }
}
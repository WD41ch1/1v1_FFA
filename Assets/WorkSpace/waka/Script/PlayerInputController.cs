using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputController : MonoBehaviour
{
    // WASDの入力値
    public Vector2 MoveInput { get; private set; }

    // マウス移動の入力値
    public Vector2 LookInput { get; private set; }

    // ジャンプが押されたか
    public bool JumpPressed { get; private set; }



    // Moveイベントから呼ばれる
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();

        Debug.Log("Move : " + MoveInput);
    }

    // Lookイベントから呼ばれる
    public void OnLook(InputAction.CallbackContext context)
    {
        LookInput = context.ReadValue<Vector2>();
    }

    // Jumpイベントから呼ばれる
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            JumpPressed = true;

            Debug.Log("Jump");
        }
    }

    // ジャンプ処理が終わったらfalseに戻す
    public void ResetJump()
    {
        JumpPressed = false;
    }
}
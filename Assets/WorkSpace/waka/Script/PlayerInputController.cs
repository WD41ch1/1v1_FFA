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

    // 壁建築ボタンが押されたか
    public bool BuildWallPressed { get; private set; }
    //階段ボタンが押されたか
    public bool BuildRampPressed {  get; private set; }

    public bool BuildFloorPressed {  get; private set; }

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

    // BuildWallイベントから呼ばれる
    public void OnBuildWall(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            BuildWallPressed = true;

            Debug.Log("Build Wall");
        }
    }

    //BuildStairイベントから呼ばれる
    public void OnBuildRamp(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            BuildRampPressed= true;
            Debug.Log("Build Ramp");
        }
    }

    public void OnBuildFloor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            BuildFloorPressed = true;
            Debug.Log("Build Floor");
        }
    }

    // ジャンプ処理が終わったらfalseに戻す
    public void ResetJump()
    {
        JumpPressed = false;
    }

    // 建築処理が終わったらfalseに戻す
    public void ResetBuildWall()
    {
        BuildWallPressed = false;
    }

    // 建築処理が終わったらfalseに戻す
    public void ResetBuildRamp()
    {
        BuildRampPressed = false;
    }

    public void ResetBuildFloor() 
    {
        BuildFloorPressed = false; 
    }
}
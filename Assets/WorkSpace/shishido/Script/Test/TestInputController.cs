using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestInputController : MonoBehaviour
{
    public PlayerInput input;

    // WASDの入力値
    public Vector2 MoveInput { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        input = new PlayerInput();

        input.Player.Enable();
        input.Combat.Enable();

        input.Player.Move.performed += OnMove;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();

        Debug.Log("Move : " + MoveInput);
    }

}

using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
[RequireComponent(typeof(UnityEngine.InputSystem.PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    // PlayerInput 컴포넌트에서 Move 액션 호출
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();
        player.SetDirectionalInput(move);
    }

    // PlayerInput 컴포넌트에서 Jump 액션 호출
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started)
            player.OnJumpInputDown();
        
        if (context.canceled)
            player.OnJumpInputUp();
    }

}

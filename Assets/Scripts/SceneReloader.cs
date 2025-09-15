using UnityEngine;
using UnityEngine.InputSystem;       // 새 인풋 시스템
using UnityEngine.SceneManagement;   // 씬 로드용

public class SceneReloader : MonoBehaviour
{
    // PlayerInput 컴포넌트에서 이벤트를 받을 함수
    public void OnRestart(InputAction.CallbackContext context)
    {
        // performed 상태일 때만 실행 (버튼 눌렀을 때)
        if (context.performed)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentScene);
        }
    }
}

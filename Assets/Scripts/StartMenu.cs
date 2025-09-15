using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    public Button startButton;

    private void Start()
    {
        startButton.onClick.AddListener(OnClickStart);
    }

    void OnClickStart()
    {
        SceneManager.LoadScene("MapDesign");
    }

}

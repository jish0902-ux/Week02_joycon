using System.Xml.Serialization;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    [SerializeField] private GameObject endingUIPanel;

    public GameObject pauseMenuUI;
    private bool isPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(Instance);
        else Instance = this;
    }
    private void Start()
    {
        pauseMenuUI.SetActive(false);
        endingUIPanel.SetActive(false);
        Time.timeScale = 1f;
    }
    private void Update()
    {
        if (endingUIPanel.activeSelf)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }
    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }
    public void ShowEndingScreen()
    {
        if (endingUIPanel != null)
        {
            endingUIPanel.SetActive(true);
            Time.timeScale = 0f;
            Debug.Log("게임 엔딩 불러옴");
        }
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }
    public void QuitGame()
    {
        Application.Quit();
    }

}

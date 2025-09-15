using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    [SerializeField] private GameObject endingUIPanel;

    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1.5f;

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

    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed) TogglePause();
    }

    public void TogglePause()
    {
        if (endingUIPanel.activeSelf) return;

        isPaused = !isPaused;
        if (isPaused) Pause();
        else Resume();

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
        StartCoroutine(EndingFadeSequenceCoroutine());
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
    private System.Collections.IEnumerator EndingFadeSequenceCoroutine()
    {
        yield return new WaitForSeconds(3f);

        float elapsedTime = 0f;
        Color color = fadeImage.color;
        fadeImage.gameObject.SetActive(true);

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }
        color.a = 1f;
        fadeImage.color = color;

        if (endingUIPanel != null)
        {
            endingUIPanel.SetActive(true);
        }
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = 1f - Mathf.Clamp01(elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }
        fadeImage.gameObject.SetActive(false);

        Time.timeScale = 0f;
        Debug.Log("°× ²ý");
    }

}

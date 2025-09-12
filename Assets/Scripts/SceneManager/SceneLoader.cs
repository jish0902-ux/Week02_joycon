using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //다음 씬으로 넘어가도 삭제하지 않도록 설정
        }
        else
        {
            Destroy(gameObject); //이미 존재하면 삭제하도록
        }
    }
    public void LoadNextScene() //인덱스 기준으로 다음 씬 로드
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex; //현재 씬 빌드 인덱스 저장
        Debug.Log($"{currentSceneIndex + 1}번 인덱스 씬을 로드합니다.");
        SceneManager.LoadScene(currentSceneIndex + 1); //다음 씬 로드
    }
    public void LoadSceneByName(string sceneName) //씬 이름으로 다음 씬 로드
    {
        Debug.Log($"{sceneName}을 로드합니다.");
        SceneManager.LoadScene(sceneName);
    }
    public void RestartScene() //현재 씬 재시작
    {
        Debug.Log("현재 씬을 재로드합니다.");
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex); //현재 씬 인덱스로 재로드
    }
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("게임을 종료합니다.");
    }

}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneUiManager : MonoBehaviour
{
    public string sceneName;

    private DataManager dm;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dm = FindAnyObjectByType<DataManager>();
    }

    public enum sampleEnum{
		
		FirstSample,
		SecondSample,
		ThirdSample,
		
	}

    /// <summary>
    /// Load the sceme
    /// </summary>
    /// <param name="sceneName"></param>
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Reset Data objects as well as all players
    /// at the end of each game
    /// </summary>
    public void Restart()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadRandomGameplayStage();
            DataManager.Instance.ResetData();
            GameManager.Instance.RestartGame();

            GameManager.Instance.isRunning = true;

            SceneManager.LoadScene("Gameplay");
        }

        SceneManager.LoadScene("Gameplay");
    }

    public void MainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetStage(-1);
            DataManager.Instance.ResetData();
            GameManager.Instance.RestartGame();

            GameManager.Instance.isRunning = true;

            SceneManager.LoadScene("MainMenu");
        }

        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

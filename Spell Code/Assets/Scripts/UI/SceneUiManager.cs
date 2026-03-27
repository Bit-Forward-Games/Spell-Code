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
        //stop repeating all sounds
        if (SFX_Manager.Instance != null)
        {
            SFX_Manager.Instance.StopRepeatingAllSounds();
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Reset Data objects as well as all players
    /// at the end of each game
    /// </summary>
    public void Restart()
    {
        //delete the old game manager
        //Destroy(GameManager.Instance);

        //create a new game manager
        //Instantiate(GameManager.Instance);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadRandomGameplayStage();
            DataManager.Instance.ResetData();
            GameManager.Instance.RestartGame();

            GameManager.Instance.isRunning = true;
            GameManager.Instance.lastSceneName = SceneManager.GetActiveScene().name;

            this.LoadScene("Gameplay");
        }

        this.LoadScene("Gameplay");
    }

    public void MainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ExecuteOrder66();
        }

        this.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

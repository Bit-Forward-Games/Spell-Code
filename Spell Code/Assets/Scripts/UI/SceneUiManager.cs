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
    /// Reset Data objects as well as all players
    /// </summary>
    /// <param name="sceneName"></param>
    public void LoadScene(string sceneName)
    {
        if (sceneName == "Gameplay")
        {
            DataManager.Instance.ResetData();
            GameManager.Instance.RestartGame();
        }
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

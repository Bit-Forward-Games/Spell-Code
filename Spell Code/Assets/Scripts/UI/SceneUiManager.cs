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

    // Update is called once per frame
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        //if there is data, save it before quitting
        if (dm.gameData.matchData.Count > 0)
        {
            dm.SaveToFile();
        }

        Application.Quit();
    }
}

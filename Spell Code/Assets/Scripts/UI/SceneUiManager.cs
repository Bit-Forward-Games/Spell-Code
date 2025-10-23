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
        if (sceneName == "Gameplay")
        {
            DataManager.Instance.ResetData();
        }
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

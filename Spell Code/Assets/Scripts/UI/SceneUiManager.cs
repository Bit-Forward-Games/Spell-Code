using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

public class SceneUiManager : MonoBehaviour
{
    public string sceneName;

    //screen transtition things
    [SerializeField]public Image ScreenCover;
    Vector3 preStartLoadPos = new Vector3(2500,0,0);
    Vector3 preEndLoadPos = new Vector3(-1500,0,0);

    Vector3 postStartLoadPos = new Vector3(-1500,0,0);
    Vector3 postEndLoadPos = new Vector3(2500,0,0);

    private bool sceneLoadInProgress;



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

        //screen transtion things
        if (ScreenCover != null)
        {
            ScreenCover.transform.localPosition = preStartLoadPos;
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(preEndLoadPos.x, 1f)
                .SetUpdate(true);
            tween.OnComplete(() =>
            {
                beforeSceneLoad?.Invoke();
                SceneManager.LoadScene(sceneName);
            });
            return;
        }

        beforeSceneLoad?.Invoke();
        SceneManager.LoadScene(sceneName);
    }

    public void RemoveScreenCover()
    {
        if (ScreenCover == null)
        {
            return;
        }

        ScreenCover.transform.localPosition = postStartLoadPos;
        Tween tween = ScreenCover.transform
            .DOLocalMoveX(postEndLoadPos.x, 1f)
            .SetUpdate(true);
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
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveToFile();
        }

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

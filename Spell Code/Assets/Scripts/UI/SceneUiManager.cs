using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;
using System.Linq.Expressions;

public class SceneUiManager : MonoBehaviour
{
    public string sceneName;

    //screen transtition things
    public Image ScreenCover;
    Vector3 preStartLoadPos = new Vector3(2500,0,0);
    Vector3 preEndLoadPos = new Vector3(-1500,0,0);

    Vector3 postStartLoadPos = new Vector3(-1500,0,0);
    Vector3 postEndLoadPos = new Vector3(2500,0,0);

    private bool sceneLoadInProgress;



    private DataManager dm;

    private void FindScreenCoverIfNeeded()
    {
        if (ScreenCover != null)
            return;

        GameObject screenCoverObject = GameObject.Find("ScreenCover");
        if (screenCoverObject != null)
        {
            ScreenCover = screenCoverObject.GetComponent<Image>();
        }
    }

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
        ApplyScreenCover(() =>
        {
            //beforeSceneLoad?.Invoke();
            BGM_Manager.Instance.StopSong();
            SceneManager.LoadScene(sceneName);
        });
    }


    public void ApplyScreenCover( Action onComplete)
    {
        //mute all gameplay sfx 
        if (SFX_Manager.Instance != null) SFX_Manager.Instance.MuteGamePlaySFX();
        GameManager.Instance.screenTransitioning = true;
        Time.timeScale = 0f;
        FindScreenCoverIfNeeded();
        //screen transtion things
        if (ScreenCover != null)
        {
            ScreenCover.transform.localPosition = preStartLoadPos;
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(preEndLoadPos.x, .5f)
                .SetUpdate(true);
            tween.OnComplete(() => {
                    onComplete();
                    Time.timeScale = 1f;

                    //unmute all gameplay sfx 
                    if (SFX_Manager.Instance != null) SFX_Manager.Instance.UnMuteGamePlaySFX();
            });
            return;
        }

        onComplete();
        Time.timeScale = 1f;

        //unmute all gameplay sfx 
        if (SFX_Manager.Instance != null) SFX_Manager.Instance.UnMuteGamePlaySFX();
    }

    public void RemoveScreenCover()
    {
        FindScreenCoverIfNeeded();

        if(ScreenCover != null)
        {
            ScreenCover.transform.localPosition = postStartLoadPos;
            //
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(postEndLoadPos.x, .5f)
                .SetDelay(.75f)
                .SetUpdate(true);

            //unmute all gameplay sfx 
            if (SFX_Manager.Instance != null) SFX_Manager.Instance.UnMuteGamePlaySFX();

            return;
        }

        //unmute all gameplay sfx 
        if (SFX_Manager.Instance != null) SFX_Manager.Instance.UnMuteGamePlaySFX();
    }

    public void RemoveScreenCover(Action onComplete)
    {
        FindScreenCoverIfNeeded();


        if(ScreenCover != null)
        {
            ScreenCover.transform.localPosition = postStartLoadPos;
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(postEndLoadPos.x, .5f)
                .SetDelay(.75f)
                .SetUpdate(true);
            tween.OnComplete(() =>
            {
                onComplete();
            });
            return;
        }
        onComplete();

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
            if (DataManager.Instance != null)
            {
                DataManager.Instance.ResetData();
            }
            GameManager.Instance.RestartGame();

            GameManager.Instance.isRunning = true;
            GameManager.Instance.lastSceneName = SceneManager.GetActiveScene().name;

            this.LoadScene("Gameplay");
        }

        this.LoadScene("Gameplay");
    }

    public void SoloLobby()
    {
        // Returning to the solo hub is an explicit cancellation point. Clear any deferred/in-flight
        // Steam invite before the screen-cover delay and persistent-manager teardown, otherwise the
        // static JOINING MATCH latch survives into the rebuilt SoloLobby UI.
        SteamLobbyManager.CancelOnlineEntryAndLeaveLobby();

#if !UNITY_EDITOR
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveToFile();
        }
#endif

        if (GameManager.Instance != null)
        {
            GameManager.Instance.sceneManager.ApplyScreenCover(() => GameManager.Instance.ExecuteOrder66("SoloLobby"));
        }
    }

    public void MainMenu()
    {
#if !UNITY_EDITOR
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveToFile();
        }
#endif

        if (GameManager.Instance != null)
        {
            GameManager.Instance.sceneManager.ApplyScreenCover(()=>GameManager.Instance.ExecuteOrder66("MainMenu"));
        }
    }

    public void QuitGame()
    {
        // clear any menu freeze, and stop play mode in the Editor where
        // Application.Quit() does nothing.
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

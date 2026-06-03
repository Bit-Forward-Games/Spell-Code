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
    [SerializeField]public Image ScreenCover;
    Vector3 preStartLoadPos = new Vector3(2500,0,0);
    Vector3 preEndLoadPos = new Vector3(-1500,0,0);

    Vector3 postStartLoadPos = new Vector3(-1500,0,0);
    Vector3 postEndLoadPos = new Vector3(2500,0,0);

    private bool sceneLoadInProgress;
    private static bool keepCameraBackgroundHiddenUntilScreenCoverRemoved;



    private DataManager dm;

    private void Awake()
    {
        HideCameraBackgroundIfWaitingForScreenCover();
    }

    private void OnEnable()
    {
        HideCameraBackgroundIfWaitingForScreenCover();
    }

    public static void KeepCameraBackgroundHiddenUntilScreenCoverRemoved()
    {
        keepCameraBackgroundHiddenUntilScreenCoverRemoved = true;
    }

    private void HideCameraBackgroundIfWaitingForScreenCover()
    {
        if (keepCameraBackgroundHiddenUntilScreenCoverRemoved)
        {
            SetCameraBackgroundImageEnabled(false);
        }
    }

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

    private void SetCameraBackgroundImageEnabled(bool enabled)
    {
        if (Camera.main == null)
            return;

        Graphic[] cameraGraphics = Camera.main.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < cameraGraphics.Length; i++)
        {
            if (IsCameraBackgroundGraphic(cameraGraphics[i]))
            {
                cameraGraphics[i].enabled = enabled;
            }
        }

        SpriteRenderer[] cameraSprites = Camera.main.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < cameraSprites.Length; i++)
        {
            Transform spriteParent = cameraSprites[i].transform.parent;
            if (cameraSprites[i].name.Contains("Background") || (spriteParent != null && spriteParent.name.Contains("Background")))
            {
                cameraSprites[i].enabled = enabled;
            }
        }
    }

    private bool IsCameraBackgroundGraphic(Graphic graphic)
    {
        if (graphic == null || graphic == ScreenCover)
            return false;

        Transform graphicTransform = graphic.transform;
        while (graphicTransform != null && graphicTransform != Camera.main.transform)
        {
            if (graphicTransform.name.Contains("Background"))
                return true;

            graphicTransform = graphicTransform.parent;
        }

        return false;
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
            SceneManager.LoadScene(sceneName);
        });
    }

    public void ApplyScreenCover( Action onComplete)
    {
        Time.timeScale = 0f;
        FindScreenCoverIfNeeded();
        //screen transtion things
        if (ScreenCover != null)
        {
            ScreenCover.transform.localPosition = preStartLoadPos;
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(preEndLoadPos.x, 1f)
                .SetUpdate(true);
            tween.OnComplete(() => {
                    onComplete();
                    Time.timeScale = 1f;
                });
            return;
        }

        onComplete();
        SetCameraBackgroundImageEnabled(false);
        Time.timeScale = 1f;
    }

    public void RemoveScreenCover()
    {
        FindScreenCoverIfNeeded();

        SetCameraBackgroundImageEnabled(false);

        if(ScreenCover != null)
        {
            ScreenCover.transform.localPosition = postStartLoadPos;
            //
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(postEndLoadPos.x, 1f)
                .SetUpdate(true);
                tween.OnComplete(() =>
                {
                    SetCameraBackgroundImageEnabled(true);
                    keepCameraBackgroundHiddenUntilScreenCoverRemoved = false;
                });
            return;
        }

        SetCameraBackgroundImageEnabled(true);
        keepCameraBackgroundHiddenUntilScreenCoverRemoved = false;

    }

    public void RemoveScreenCover(Action onComplete)
    {
        FindScreenCoverIfNeeded();

        SetCameraBackgroundImageEnabled(false);

        if(ScreenCover != null)
        {
            ScreenCover.transform.localPosition = postStartLoadPos;
            Tween tween = ScreenCover.transform
                .DOLocalMoveX(postEndLoadPos.x, 1f)
                .SetUpdate(true);
            tween.OnComplete(() =>
            {
                SetCameraBackgroundImageEnabled(true);
                keepCameraBackgroundHiddenUntilScreenCoverRemoved = false;
                onComplete();
            });
            return;
        }
        SetCameraBackgroundImageEnabled(true);
        keepCameraBackgroundHiddenUntilScreenCoverRemoved = false;
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
#if !UNITY_EDITOR
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveToFile();
        }
#endif

        if (GameManager.Instance != null)
        {
            KeepCameraBackgroundHiddenUntilScreenCoverRemoved();
            GameManager.Instance.sceneManager.ApplyScreenCover(()=>GameManager.Instance.ExecuteOrder66());
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

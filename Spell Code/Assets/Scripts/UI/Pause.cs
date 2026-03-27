using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public GameObject optionsMenu;
    public GameManager gameManager;
    public bool paused;
    public bool options;
    public Slider volumeSlider;
    public Slider sfxSlider;
    public AudioSource musicAudioSource;
    public AudioSource sfxAudioSource;
    public bool shakeEnabled = true;
    public bool dynamicCameraOverride = true;
    private SceneUiManager sceneUiManager;

    private void Start()
    {
        gameManager = GameManager.Instance;
        //find sceneUiManager
        sceneUiManager = GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>();
        Resume();
    }

    // Update is called once per frame
    void Update()
    {
        if (musicAudioSource == null)
            musicAudioSource = GameObject.Find("pfb_BGM_Manager").gameObject.GetComponent<AudioSource>();
            
        if (sfxAudioSource == null)
            sfxAudioSource = GameObject.Find("pfb_SFX_Manager").gameObject.GetComponent<AudioSource>();

        if (!gameManager.isOnlineMatchActive)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (paused)
                {
                    Resume();
                }
                else
                {
                    Pausing();
                }
            }
        }
    }

    public void Resume()
    {
        paused = false;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        Time.timeScale = 1f;    
    }

    public void Pausing()
    {
        paused = true;
        options = false;
        pausemenu.SetActive(true);
        optionsMenu.SetActive(false);
        Time.timeScale = 0f;
    }

    public void Options()
    {
        options = true;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ReturnToLobby()
    {
        //Resume game
        Resume();

        //Restart the game back at the lobby
        sceneUiManager.MainMenu();
    }

    public void QuitGame()
    {
        //save data
        DataManager.Instance.SaveToFile();

        //quit the game
        Debug.Log("Quitting Spell Code SlingerZ");
        Application.Quit();
    }

    public void MusicVolume()
    {
        musicAudioSource.volume = volumeSlider.value;
    }

    public void SFXVolume()
    {
        sfxAudioSource.volume = sfxSlider.value;
    }

    public void ToggleCameraShake()
    {
        shakeEnabled = !shakeEnabled;
    }

    public void ToggleDynamicCamera()
    {
        dynamicCameraOverride = !dynamicCameraOverride;
    }
}

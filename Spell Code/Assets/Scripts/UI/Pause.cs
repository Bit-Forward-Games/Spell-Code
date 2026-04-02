using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public GameObject optionsMenu;
    public GameObject darkPanel;
    public GameManager gameManager;
    public int playerPauseIndex;
    public bool paused;
    public bool options;
    public Slider volumeSlider;
    public Slider sfxSlider;
    public AudioSource musicAudioSource;
    public AudioSource sfxAudioSource;
    public bool shakeEnabled = true;
    public bool dynamicCameraOverride = true;
    private SceneUiManager sceneUiManager;

    public GameObject _pauseMenuFirst;
    public GameObject _optionsMenuFirst;

    public Toggle relativeInputToggleGraphic;
    public Toggle codeInputToggleGraphic;

    public bool UIRelativeInput
    {
        get { return gameManager.players[playerPauseIndex].relativeInputs; }
        set 
        {
            relativeInputToggleGraphic.isOn = gameManager.players[playerPauseIndex].relativeInputs;
            gameManager.players[playerPauseIndex].relativeInputs = value; 
        }
    }

    public bool UIToggleCodeInput
    {
        get { return gameManager.players[playerPauseIndex].toggleCodeInput; }
        set 
        {
            codeInputToggleGraphic.isOn = gameManager.players[playerPauseIndex].toggleCodeInput;
            gameManager.players[playerPauseIndex].toggleCodeInput = value; 
        }
    }

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
    }

    public void Resume()
    {
        paused = false;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        darkPanel.SetActive(false);

        EventSystem.current.SetSelectedGameObject(null);

        Time.timeScale = 1f;    
    }

    public void Pausing()
    {
        paused = true;
        options = false;
        pausemenu.SetActive(true);
        optionsMenu.SetActive(false);
        darkPanel.SetActive(true);

        EventSystem.current.SetSelectedGameObject(_pauseMenuFirst);

        Time.timeScale = 0f;
    }

    public void Options()
    {
        options = true;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(_optionsMenuFirst);

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

    public void ToggleRelativeInput()
    {
        UIRelativeInput = !UIRelativeInput;
    }

    public void ToggleCodeInput()
    {
        UIToggleCodeInput = !UIToggleCodeInput;
    }
}

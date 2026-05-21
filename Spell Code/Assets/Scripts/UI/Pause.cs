using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public GameObject optionsMenu;
    public GameObject controlsMenu;
    public GameObject spellsMenu;
    public GameObject darkPanel;
    public GameObject[] spellGlossaryPanel;
    public GameManager gameManager;
    public int playerPauseIndex;
    public bool paused;
    public bool options;
    public bool controls;
    public bool spells;
    public AudioMixer masterAudioMixer;
    public AudioMixer musicAudioMixer;
    public AudioMixer sfxAudioMixer;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public bool shakeEnabled = true;
    public bool dynamicCameraOverride = true;
    private SceneUiManager sceneUiManager;

    public GameObject _pauseMenuFirst;
    public GameObject _optionsMenuFirst;
    public GameObject _controlsMenuFirst;
    public GameObject _spellsMenuFirst;

    public Toggle relativeInputToggleGraphic;
    public Toggle codeInputToggleGraphic;
    public Toggle tapJumpToggleGraphic;

    public TextMeshProUGUI displaySpellName;
    public TextMeshProUGUI displaySpellDescription;

    private int tab = 0;

    public class Row
    {
        public SpellData[] spells;
    }

    public Row[] grid = new Row[4];


    public bool UIRelativeInput
    {
        get { return gameManager.players[playerPauseIndex].relativeInputs; }
        set 
        {
            gameManager.players[playerPauseIndex].relativeInputs = value;
        }
    }

    public bool UIToggleCodeInput
    {
        get { return gameManager.players[playerPauseIndex].toggleCodeInput; }
        set 
        {
            gameManager.players[playerPauseIndex].toggleCodeInput = value; 
        }
    }
    
    public bool UITapJump
    {
        get { return gameManager.players[playerPauseIndex].tapJump; }
        set 
        {
            gameManager.players[playerPauseIndex].tapJump = value; 
        }
    }

    private InputSystem_Actions input; // your generated class name

    void Awake()
    {
        input = new InputSystem_Actions();
        
        Brand[] brandPerRow = { Brand.DemonX, Brand.BigStox, Brand.Killeez, Brand.VWave};

        for (int i = 0; i < 4; i++)
        {
            grid[i] = new Row();

            List<SpellData> rowSpells = new List<SpellData>();

            foreach (SpellData spell in SpellDictionary.Instance.spellList)
            {
                if (spell != null && System.Array.Exists(spell.brands, b => b == brandPerRow[i]))
                {
                    rowSpells.Add(spell);
                }
            }

            grid[i].spells = rowSpells.ToArray();
        }
    }

    void Update()
    {
        if (spells)
        {
            SpellGlossaryNavigation();
        }
        
        if (tab > 0 )
        {
            displaySpellName.text = grid[tab - 1].spells[0].spellName;
            displaySpellDescription.text = "Description: " + grid[tab - 1].spells[0].description;
        }
    }

    void OnEnable()  { input.Enable(); }
    void OnDisable() { input.Disable(); }

    private void Start()
    {
        gameManager = GameManager.Instance;
        //find sceneUiManager
        sceneUiManager = GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>();
        Resume();
    }

    public void Resume()
    {
        paused = false;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        darkPanel.SetActive(false);

        EventSystem.current.SetSelectedGameObject(null);

        Time.timeScale = 1f;    
    }

    public void Pausing()
    {
        paused = true;
        options = false;
        controls = false;
        spells = false;
        pausemenu.SetActive(true);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        spellsMenu.SetActive(false);
        darkPanel.SetActive(true);

        relativeInputToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].relativeInputs);
        codeInputToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].toggleCodeInput);

        EventSystem.current.SetSelectedGameObject(_pauseMenuFirst);

        Time.timeScale = 0f;
    }

    public void Options()
    {
        options = true;
        controls = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(true);
        controlsMenu.SetActive(false);

        EventSystem.current.SetSelectedGameObject(_optionsMenuFirst);

        Time.timeScale = 0f;
    }

    public void Controls()
    {
        controls = true;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(true);

        EventSystem.current.SetSelectedGameObject(_controlsMenuFirst);

        Time.timeScale = 0f;
    }
    
    public void Spells()
    {
        spells = true;
        controls = false;
        options = false;
        spellsMenu.SetActive(true);
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);

        EventSystem.current.SetSelectedGameObject(_spellsMenuFirst);

        Time.timeScale = 0f;
    }

    public void SpellGlossaryNavigation()
    {
        input.UI.Navigate.performed += ctx =>
        {
            Vector2 nav = ctx.ReadValue<Vector2>();

            if (nav.y > 0) Debug.Log("Up");
            if (nav.y < 0) Debug.Log("Down");
            if (nav.x < 0) 
            {
                if (tab == 0) tab = 4;
                else tab--;
            }
            if (nav.x > 0) 
            {
                if (tab == 4) tab = 0;
                else tab++;
            }
            
            ActivateOnly(tab);
        };

    }

    void ActivateOnly(int index)
    {
        for (int i = 0; i < spellGlossaryPanel.Length; i++)
        {
            spellGlossaryPanel[i].SetActive(i == index);
        }
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
        musicAudioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolumeSlider.value) * 20f);
    }

    public void SFXVolume()
    {
        musicAudioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolumeSlider.value) * 20f);
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

    public void ToggleTapJump()
    {
        UITapJump = !UITapJump;
    }
}

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
    public TextMeshProUGUI spellSelectedText;
    public Image spellSelectedBorder;
    public RectTransform spellSelectedBorderTransform;
    public GameObject unselectedSpell;
    public GameObject spellListParent;
    public GameObject[] spellGlossaryList;

    private int tab = 0;
    private int selectedSpell;

    public class Column
    {
        public SpellData[] spells;
    }

    public Column[] grid = new Column[6];


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

    private InputSystem_Actions input;

    void OnEnable()  { input.Enable(); }
    void OnDisable() { input.Disable(); }

    void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        //find sceneUiManager
        sceneUiManager = GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>();
        Resume();

        spellGlossaryList = new GameObject[SpellDictionary.Instance.spellList.Count];

        for (int i = 0; i < SpellDictionary.Instance.spellList.Count; i++)
        {
            SpellData spell = SpellDictionary.Instance.spellList[i];
            spellGlossaryList[i] = Instantiate(unselectedSpell, spellListParent.transform);
            Image spellGraphic = spellGlossaryList[i].GetComponent<Image>();
            TextMeshProUGUI spellNameText = spellGlossaryList[i].GetComponentInChildren<TextMeshProUGUI>();

            switch (spell.brands[0])
            {
                case Brand.VWave:
                    spellGraphic.color = GameManager.colors["green"];
                    break;
                case Brand.BigStox:
                    spellGraphic.color = GameManager.colors["blue"];
                    break;
                case Brand.DemonX:
                    spellGraphic.color = GameManager.colors["red"];
                    break;
                case Brand.Killeez:
                    spellGraphic.color = GameManager.colors["yellow"];
                    break;
            }

            spellNameText.text = spell.spellName;
            
            spellGlossaryList[i].SetActive(false);
        }
    }

    void Update()
    {
        if (spells)
        {
            SpellGlossaryNavigation();
        }

        if (input.UI.Cancel.WasPressedThisFrame())
        {
            Resume();
        }
    }

    public void Resume()
    {
        paused = false;
        options = false;
        spells = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        darkPanel.SetActive(false);
        spellsMenu.SetActive(false);

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

        tab = 0;

        Brand[] brandPerColumn = { Brand.None, Brand.DemonX, Brand.BigStox, Brand.Killeez, Brand.VWave };

        for (int i = 0; i < 6; i++)
        {
            grid[i] = new Column();

            List<SpellData> columnSpells = new List<SpellData>();
            
            if (i == 0)
            {
                List<SpellData> mySpells = gameManager.players[playerPauseIndex].spellList;
                
                if (mySpells != null)
                    foreach (SpellData spell in gameManager.players[playerPauseIndex].spellList)
                        if (spell != null) columnSpells.Add(spell);
            }
            else if (i == 5)
            {
                foreach (SpellData spell in SpellDictionary.Instance.spellList)
                {
                    if (spell != null)
                    {
                        columnSpells.Add(spell);
                    }
                }
            }
            else
            {
                foreach (SpellData spell in SpellDictionary.Instance.spellList)
                {
                    if (spell != null && System.Array.Exists(spell.brands, b => b == brandPerColumn[i]))
                    {
                        columnSpells.Add(spell);
                    }
                }
            }
            grid[i].spells = columnSpells.ToArray();
        }

        EventSystem.current.SetSelectedGameObject(_spellsMenuFirst);

        Time.timeScale = 0f;
    }

    public void SpellGlossaryNewTab()
    {
        List<GameObject> columnSpellList = new List<GameObject>();
        selectedSpell = 0;
        
        spellSelectedBorderTransform.anchoredPosition = new Vector2(spellSelectedBorderTransform.anchoredPosition.x, 200f);
        
        int j = 0;
        
        for (int i = 0; i < spellGlossaryList.Length ; i++)
        {
            if (j >= grid[tab].spells.Length)
            {
                spellGlossaryList[i].SetActive(false);
                continue;
            }
            
            if (spellGlossaryList[i].GetComponentInChildren<TextMeshProUGUI>().text == grid[tab].spells[j].spellName)
            {
                spellGlossaryList[i].SetActive(true);
                spellGlossaryList[i].transform.position = new Vector2(spellGlossaryList[i].transform.position.x, spellListParent.transform.position.y - (j * 200));
                j++;
            }
            else
            {
                spellGlossaryList[i].SetActive(false);
            }
        }
    }

    public void SpellGlossaryListSelection(float one)
    {
        spellSelectedBorderTransform.anchoredPosition += new Vector2(0, one * 70f);
    }

    public void SpellGlossaryNavigation()
    {

        input.UI.Navigate.performed += ctx =>
        {
            Vector2 nav = ctx.ReadValue<Vector2>();

            if (nav.y > 0) 
            {
                if (selectedSpell > 0) 
                {
                    selectedSpell--;
                    SpellGlossaryListSelection(1);
                }
            }
            if (nav.y < 0) 
            {
                if (selectedSpell < grid[tab].spells.Length - 1) 
                {
                    selectedSpell++;
                    SpellGlossaryListSelection(-1);
                }
            }
            if (nav.x < 0) 
            {
                if (tab == 0) tab = 5;
                else tab--;
                SpellGlossaryNewTab();
            }
            if (nav.x > 0) 
            {
                if (tab == 5) tab = 0;
                else tab++;
                SpellGlossaryNewTab();
            }
            
            ActivateOnly(tab);
        };

        if (grid[tab] != null && grid[tab].spells.Length > 0)
        {
            displaySpellName.text = grid[tab].spells[selectedSpell].spellName;
            displaySpellDescription.text = "Description: " + grid[tab].spells[selectedSpell].description;
            spellSelectedText.text = grid[tab].spells[selectedSpell].spellName;

            if (grid[tab].spells[selectedSpell].brands != null && grid[tab].spells[selectedSpell].brands.Length > 0)
            {
                switch (grid[tab].spells[selectedSpell].brands[0])
                {
                    case Brand.VWave:
                        spellSelectedBorder.color = GameManager.colors["green"];
                        break;
                    case Brand.BigStox:
                        spellSelectedBorder.color = GameManager.colors["blue"];
                        break;
                    case Brand.DemonX:
                        spellSelectedBorder.color = GameManager.colors["red"];
                        break;
                    case Brand.Killeez:
                        spellSelectedBorder.color = GameManager.colors["yellow"];
                        break;
                }
            }
            
        }
        else
        {
            displaySpellName.text = "none";
            displaySpellDescription.text = "none";
            spellSelectedText.text = "none";
            spellSelectedBorder.color = new Color32(255, 255, 255, 255);
        }
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

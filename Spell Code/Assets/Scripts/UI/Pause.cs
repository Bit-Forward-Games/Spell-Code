using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.EventSystems;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using GifImporter; 

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public GameObject optionsMenu;
    public GameObject volumeMenu;
    public GameObject displayMenu;
    public GameObject controlsMenu;
    public GameObject spellsMenu;
    public GameObject darkPanel;
    public GameObject[] spellGlossaryPanel;
    public GameManager gameManager;
    public int playerPauseIndex;
    public bool paused;
    public bool options;
    public bool volumeOptions;
    public bool displayOptions;
    public bool controls;
    public bool spells;
    public AudioMixer masterAudioMixer;
    public AudioMixer musicAudioMixer;
    public AudioMixer sfxAudioMixer;
    public AudioMixer menuSfxAudioMixer;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public bool screenShake = true;
    public bool dynamicCameraOverride = true;
    public Toggle screenShakeToggle;
    public Toggle dynamicCameraToggle;
    private SceneUiManager sceneUiManager;
    public TempUIScript uiScript;
    private const float MinMixerVolume = 0.0001f;
 
    public GameObject _pauseMenuFirst;
    public GameObject _optionsMenuFirst;
    public GameObject _controlsMenuFirst;
    public GameObject _volumeMenuFirst;
    public GameObject _displayMenuFirst;
    public GameObject _spellsMenuFirst;
 
    public TextMeshProUGUI playerPausedText;
    public Toggle relativeInputToggleGraphic;
    public Toggle codeInputToggleGraphic;
    public Toggle tapJumpToggleGraphic;
    public Toggle vibeCodingToggleGraphic;
    public Toggle downJumpSlideToggleGraphic;
    public static readonly string[] BannedRebindInputs =
    {
        "<Keyboard>/escape",
        "<Keyboard>/enter",
        "<Keyboard>/numpadEnter",
        "<Keyboard>/w",
        "<Keyboard>/a",
        "<Keyboard>/s",
        "<Keyboard>/d",
        "<Gamepad>/start",
        "<Gamepad>/select",
        "<Gamepad>/leftStickPress",
        "<Gamepad>/rightStickPress",
        "<Gamepad>/D-Pad/up",
        "<Gamepad>/D-Pad/down",
        "<Gamepad>/D-Pad/left",
        "<Gamepad>/D-Pad/right",
        "<Mouse>/*",
        "<Pointer>/*",
        "<Touchscreen>/*"
    };

    [Header("Spell Glossary Variables")]
 
    private string[] brandName = {"MySpells", "Resources", "DemonX", "BigStoX", "Killeez", "VWave"};
    public TextMeshProUGUI spellAddress;
    public TextMeshProUGUI displaySpellName;
    public TextMeshProUGUI displaySpellDescription;
    public TextMeshProUGUI spellSelectedText;
    public TextMeshProUGUI cooldownText;
    public TextMeshProUGUI inputText;
    public Image spellSelectedBorder;
    public RectTransform spellSelectedBorderTransform;
    public RectTransform descriptionPanel;
    public RectTransform gifDisplayPanel;
    public RectTransform gifTransform;
    public GameObject unselectedSpell;
    public GameObject spellListParent;
    public GameObject[] spellGlossaryList;
    public List<GameObject> spellTabList = new List<GameObject>();
    public Image colorLayer;
    public Image colorLayer2;
    public Image colorLayer3;
    public Image colorLayer4;
    public GifPlayer gifPlayer;
    public Sprite[] fellas;
    public GameObject fella;
    private bool showDescription = true;
 
    private int tab = 0;
    private int selectedSpell;
    private float spellListInitialY;
    private int openedFrame = -1;
 
    // Cooldown to prevent held-stick from firing every frame
    private float navCooldown = 0f;
    private const float NAV_COOLDOWN_TIME = 0.2f;
    // Track last frame's nav value to detect fresh presses
    private Vector2 lastNavValue = Vector2.zero;

    public TMP_FontAsset digitalBorderedFont;
    public TMP_FontAsset digitalNormalFont;
 
    public class Column
    {
        public SpellData[] spells;
    }
 
    public Column[] grid = new Column[6];
 
 
    public bool UIRelativeInput
    {
        get { return GetRelativeInputOption(); }
        set 
        {
            SetPauseControlOptions(value, UIToggleCodeInput, UITapJump, UIVibeCode, UIDownJumpSlide);
        }
    }
 
    public bool UIToggleCodeInput
    {
        get { return GetToggleCodeInputOption(); }
        set 
        {
            SetPauseControlOptions(UIRelativeInput, value, UITapJump, UIVibeCode, UIDownJumpSlide);
        }
    }
    
    public bool UITapJump
    {
        get { return GetTapJumpOption(); }
        set 
        {
            SetPauseControlOptions(UIRelativeInput, UIToggleCodeInput, value, UIVibeCode, UIDownJumpSlide);
        }
    }

    public bool UIVibeCode
    {
        get { return GetVibeCodingOption(); }
        set 
        {
            SetPauseControlOptions(UIRelativeInput, UIToggleCodeInput, UITapJump, value, UIDownJumpSlide);
        }
    }

    public bool UIDownJumpSlide
    {
        get { return GetDownJumpSlideOption(); }
        set 
        {
            SetPauseControlOptions(UIRelativeInput, UIToggleCodeInput, UITapJump, UIVibeCode, value);
        }
    }
 
    private InputSystem_Actions input;
    private SCMaster scInput;
    private InputSystemUIInputModule uiInputModule;
    private InputActionAsset scopedUiActionsAsset;
    private ReadOnlyArray<InputDevice>? previousUiInputDevices;
    private bool uiInputDevicesScoped;
    private InputActionRebindingExtensions.RebindingOperation activeRebindOperation;
    private InputAction activeRebindAction;
    private bool activeRebindActionWasEnabled;
 
    void OnEnable()  
    { 
        input.Enable(); 
        scInput.Enable(); 
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDisable() 
    { 
        CancelActiveRebind();
        RestoreUiInputDevices();
        input.Disable(); 
        scInput.Disable(); 
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
 
    void Awake()
    {
        input = new InputSystem_Actions();
        scInput = new SCMaster();
        LoadSettings();
    }
 
    private void Start()
    {
        gameManager = GameManager.Instance;
        sceneUiManager = GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>();
 
        Resume();
 
        spellListInitialY = spellListParent.transform.position.y;
 
        spellGlossaryList = new GameObject[SpellDictionary.Instance.spellList.Count];
 
        for (int i = 0; i < SpellDictionary.Instance.spellList.Count; i++)
        {
            SpellData spell = SpellDictionary.Instance.spellList[i];
            spellGlossaryList[i] = Instantiate(unselectedSpell, spellListParent.transform);
            Transform childTransform = spellGlossaryList[i].transform.Find("Panel Color");

            GameObject panel = null;

            if (childTransform != null)
            {
                panel = childTransform.gameObject;
            }
            Image panelColor = panel.GetComponent<Image>();
            Image spellGraphic = spellGlossaryList[i].GetComponent<Image>();
            TextMeshProUGUI spellNameText = spellGlossaryList[i].GetComponentInChildren<TextMeshProUGUI>();
 
            switch (spell.brands[0])
            {
                case Brand.VWave:
                    panelColor.color = GameManager.colors["green"];
                    break;
                case Brand.BigStox:
                    panelColor.color = GameManager.colors["blue"];
                    break;
                case Brand.DemonX:
                    panelColor.color = GameManager.colors["red"];
                    break;
                case Brand.Killeez:
                    panelColor.color = GameManager.colors["yellow"];
                    break;
            }
 
            spellNameText.text = spell.spellName;
            
            spellGlossaryList[i].SetActive(false);
        }

        spellListParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(spellSelectedBorderTransform.anchoredPosition.x, 280f);
        ChangeSpellPanelView(-544f, new Vector2(606f, -20f), new Vector2(1384f, 652f), new Vector2(507.66f, -38f), new Vector2(0.57f, 0.57f), new Vector2(409f, 228f));
        RefreshDynamicCameraOptionForScene();
    }
 
    void Update()
    {
        ScopeUiInputToPausePlayer();

        GameObject selectedGO = EventSystem.current.currentSelectedGameObject;

        if (spells)
        {
            UpdateSpellDisplay();
        }
        SpellGlossaryNavigation();
 
        if (paused && Time.frameCount != openedFrame && WasPausePlayerCancelPressedThisFrame())
        {
            Resume();
        }

        if (WasPausePlayerBackPressedThisFrame() && paused)
        {
            Back();
        }

        if (WasPausePlayerSubmitPressedThisFrame() && !spells && paused)
        {
            TriggerSelectedButton();
        }

        if (!uiScript.soloGamemodesMenuOpened && !paused && !uiScript.tutorialPromptMenuOpened) 
        {
            Time.timeScale = 1f;
            EventSystem.current.SetSelectedGameObject(null);
            RestoreUiInputDevices();
        }
    }
 
    private void SpellGlossaryNavigation()
    {
        Vector2 nav = GetPausePlayerNavigation();
 
        // Tick down cooldown using unscaled time so it works while paused (timeScale = 0)
        navCooldown -= Time.unscaledDeltaTime;
 
        // Only act on a fresh directional press OR if cooldown has expired while stick is held
        bool freshPress = (nav != Vector2.zero && lastNavValue == Vector2.zero);
        bool heldAndReady = (nav != Vector2.zero && navCooldown <= 0f);
 
        if (freshPress || heldAndReady)
        {
            navCooldown = NAV_COOLDOWN_TIME;

            // regular pause menu navigation sound
            if (!spells && paused)
            {
                //play the neutral select sound
                SFX_Manager.Instance.PlayMenuSound("Neutral Select");
            }
 
            if (nav.y > 0)
            {
                if (spells && selectedSpell > 0) 
                {
                    //play the neutral select sound
                    SFX_Manager.Instance.PlayMenuSound("Neutral Select");

                    selectedSpell--;
                    SpellGlossaryListSelection(1);
                }
            }
            else if (nav.y < 0)
            {
                if (spells && selectedSpell < grid[tab].spells.Length - 1)
                {
                    //play the neutral select sound
                    SFX_Manager.Instance.PlayMenuSound("Neutral Select");

                    selectedSpell++;
                    SpellGlossaryListSelection(-1);
                }
            }
 
            if (nav.x < 0)
            {
                if (spells)
                {
                    //play the tab select sound
                    SFX_Manager.Instance.PlayMenuSound("Tab Select");

                    tab = (tab == 0) ? 5 : tab - 1;
                    SpellGlossaryNewTab();
                    SpellSelectBorderAnimation(spellGlossaryPanel[tab].GetComponent<RectTransform>(), 1f);
                }
            }
            else if (nav.x > 0)
            {
                if (spells)
                {
                    //play the tab select sound
                    SFX_Manager.Instance.PlayMenuSound("Tab Select");

                    tab = (tab == 5) ? 0 : tab + 1;
                    SpellGlossaryNewTab();
                    SpellSelectBorderAnimation(spellGlossaryPanel[tab].GetComponent<RectTransform>(), 1f);
                }
            }
 
            ActivateOnly(tab);
        }
 
        // Reset cooldown faster on fresh release so next press is always instant
        if (nav == Vector2.zero) navCooldown = 0f;
 
        lastNavValue = nav;

        if (grid[tab] != null && grid[tab].spells.Length > 0 && grid[tab].spells[selectedSpell] != null)
        {
            spellAddress.text = "http://www.myspellcodelist.com/"
                + brandName[tab].Replace(" ", "") + "/"
                + grid[tab].spells[selectedSpell].spellName.Replace(" ", "");
        }
        else spellAddress.text = "http://www.myspellcodelist.com/";
    }

    private Brand lastFellaBrand = (Brand)(-1);
 
    private void UpdateSpellDisplay()
    {
        if (WasPausePlayerSubmitPressedThisFrame() && spells)
        {
            if (!showDescription)
                ChangeSpellPanelView(-544f, new Vector2(606f, -20f), new Vector2(1384f, 652f), new Vector2(507.66f, -38f), new Vector2(0.57f, 0.57f), new Vector2(409f, 228f));
            else if (showDescription)
                ChangeSpellPanelView(-2500f, new Vector2(753.12f, -248.62f), new Vector2(1813.76f, 1189.24f), new Vector2(615f, -305f), new Vector2(1.1f, 1.1f), new Vector2(396f, 152f));
        }
        
        if (grid[tab] != null && grid[tab].spells.Length > 0)
        {
            displaySpellName.text = grid[tab].spells[selectedSpell].spellName;
            displaySpellDescription.text = "Description: " + grid[tab].spells[selectedSpell].description;
            spellSelectedText.text = grid[tab].spells[selectedSpell].spellName;
            gifPlayer.Gif = grid[tab].spells[selectedSpell].SpellGIF;
            cooldownText.text = "Cooldown:  " + Mathf.FloorToInt((float)grid[tab].spells[selectedSpell].cooldown/60f) + "s";
            inputText.text = "Input:  " + PlayerController.ConvertCodeToString(grid[tab].spells[selectedSpell].spellInput);
            
            if (grid[tab].spells[selectedSpell].brands[0] == lastFellaBrand) return;
            else
            {
                lastFellaBrand = grid[tab].spells[selectedSpell].brands[0];
                fella.GetComponent<Image>().sprite = fellas[(int)grid[tab].spells[selectedSpell].brands[0] - 1];

                RectTransform fellaTransform = fella.GetComponent<RectTransform>();

                fellaTransform.localScale = new Vector3(fellaTransform.localScale.x, 0f, fellaTransform.localScale.z);
                fellaTransform
                    .DOScaleY(1f, 0.15f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
 
            if (grid[tab].spells[selectedSpell].brands != null && grid[tab].spells[selectedSpell].brands.Length > 0)
            {
                switch (grid[tab].spells[selectedSpell].brands[0])
                {
                    case Brand.VWave:
                        spellSelectedBorder.color = GameManager.colors["green"];
                        colorLayer.color = GameManager.colors["green"];
                        colorLayer2.color = GameManager.colors["green"];
                        colorLayer3.color = GameManager.colors["green"];
                        colorLayer4.color = GameManager.colors["green"];
                        break;
                    case Brand.BigStox:
                        spellSelectedBorder.color = GameManager.colors["blue"];
                        colorLayer.color = GameManager.colors["blue"];
                        colorLayer2.color = GameManager.colors["blue"];
                        colorLayer3.color = GameManager.colors["blue"];
                        colorLayer4.color = GameManager.colors["blue"];
                        break;
                    case Brand.DemonX:
                        spellSelectedBorder.color = GameManager.colors["red"];
                        colorLayer.color = GameManager.colors["red"];
                        colorLayer2.color = GameManager.colors["red"];
                        colorLayer3.color = GameManager.colors["red"];
                        colorLayer4.color = GameManager.colors["red"];
                        break;
                    case Brand.Killeez:
                        spellSelectedBorder.color = GameManager.colors["yellow"];
                        colorLayer.color = GameManager.colors["yellow"];
                        colorLayer2.color = GameManager.colors["yellow"];
                        colorLayer3.color = GameManager.colors["yellow"];
                        colorLayer4.color = GameManager.colors["yellow"];
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
 
    public void Resume()
    {
        if (paused)
        {
            //play the resume sfx
            SFX_Manager.Instance.PlayMenuSound("Resume");
        }

        paused = false;
        options = false;
        controls = false;
        spells = false;
        openedFrame = -1;
        uiScript.tutorialPromptMenuOpened = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        volumeMenu.SetActive(false);
        displayMenu.SetActive(false);
        darkPanel.SetActive(false);
        spellsMenu.SetActive(false);
        RestoreUiInputDevices();
 
        EventSystem.current.SetSelectedGameObject(null);
        SaveSettings(); 
        Time.timeScale = 1f;

        if (uiScript.soloGamemodesMenuOpened) StartCoroutine(BackToGameModeSelector());

        //unmute all sfx
        SFX_Manager.Instance.UnMuteGamePlaySFX();

        //apply volume
        //SFXVolume();
    }

    public void Back()
    {
        if (Time.frameCount != openedFrame)
        {
            if (controls || volumeOptions || displayOptions)
            {
                controlsMenu.SetActive(false);
                volumeMenu.SetActive(false);
                displayMenu.SetActive(false);
                Options();
            }
            else if (options || spells) Pausing();
            else Resume();
        }
    }

    public IEnumerator BackToGameModeSelector()
    {
        yield return new WaitForSeconds(0.02f);
        Time.timeScale = 0f;
        EventSystem.current.SetSelectedGameObject(uiScript._soloGamemodesMenuFirst);
    }

    public void SaveSettings()
    {
        SettingsManager settingsManager = SettingsManager.Instance;
        if (settingsManager == null)
        {
            return;
        }

        settingsManager.SetDynamicCamera(dynamicCameraOverride);
        settingsManager.SetScreenshake(screenShake);
        settingsManager.SetFullscreen(true);
        if (masterVolumeSlider != null) settingsManager.SetMasterVolume(masterVolumeSlider.value);
        if (musicVolumeSlider != null) settingsManager.SetMusicVolume(musicVolumeSlider.value);
        if (sfxVolumeSlider != null) settingsManager.SetSfxVolume(sfxVolumeSlider.value);
        settingsManager.Save();

        if (!IsDynamicCameraForcedScene())
        {
            settingsManager.SetDynamicCamera(dynamicCameraOverride);
        }
        settingsManager.SetScreenshake(screenShake);
        settingsManager.SetFullscreen(true);
        if (masterVolumeSlider != null) settingsManager.SetMasterVolume(masterVolumeSlider.value);
        if (musicVolumeSlider != null) settingsManager.SetMusicVolume(musicVolumeSlider.value);
        if (sfxVolumeSlider != null) settingsManager.SetSfxVolume(sfxVolumeSlider.value);
    }

    public void LoadSettings()
    {
        SettingsManager settingsManager = SettingsManager.Instance;
        if (settingsManager == null)
        {
            return;
        }

        settingsManager.Load();
        GameSettingsData settings = settingsManager.Settings;
        dynamicCameraOverride = settings.dynamicCamera;
        screenShake = settings.screenshake;
        RefreshDynamicCameraOptionForScene();
        if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(screenShake);
        //if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
        //if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
        if (masterVolumeSlider != null) masterVolumeSlider.value = settings.masterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = settings.musicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = settings.sfxVolume;
        MasterVolume();
        MusicVolume();
        SFXVolume();
        Debug.Log("HERE | LoadSettings | saved music volume = " + settings.musicVolume + ", and saved sfx volume = " + settings.sfxVolume);
        //ApplyMusicMixerVolume(settings.musicVolume);
        //ApplySfxMixerVolume(settings.sfxVolume);
    }
 
    public void Pausing()
    {
        if(spells || options || controls)
        {
            //play the pause sfx
            SFX_Manager.Instance.PlayMenuSound("Negative Select");
        }

        if (!paused)
        {
            //play the pause sfx
            SFX_Manager.Instance.PlayMenuSound("Pause");
        }

        paused = true;
        options = false;
        controls = false;
        spells = false;
        openedFrame = Time.frameCount;
        pausemenu.SetActive(true);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        volumeMenu.SetActive(false);
        displayMenu.SetActive(false);
        spellsMenu.SetActive(false);
        darkPanel.SetActive(true);

        playerPausedText.text = "P" + (playerPauseIndex + 1) + (IsOnlineMatchActive() ? " Menu" : " Paused");
        ScopeUiInputToPausePlayer();
 
        RefreshControlToggleGraphics();
        
 
        StartCoroutine(SelectFirst(_pauseMenuFirst));
 
        SetMenuTimeScale();

        //mute all gameplay sfx but not menu sfx
        SFX_Manager.Instance.MuteGamePlaySFX();
        Debug.Log("HERE");
        LoadSettings();
        //menuSfxAudioMixer.SetFloat("MenuSFXVolume", Mathf.Log10(sfxVolumeSlider.value) * 20f);
        //sfxAudioMixer.SetFloat("SFXVolume", Mathf.Log10(0.00001f) * 20f);
    }
 
    public void Options()
    {
        RefreshDynamicCameraOptionForScene();

        options = true;
        controls = false;
        displayOptions = false;
        volumeOptions = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(true);
        controlsMenu.SetActive(false);
        displayMenu.SetActive(false);
        volumeMenu.SetActive(false);
 
        SetMenuTimeScale();

        StartCoroutine(SelectFirst(_optionsMenuFirst));

        if (masterVolumeSlider != null) masterVolumeSlider.value = SettingsManager.Instance.Settings.masterVolume;
        if (musicVolumeSlider != null) musicVolumeSlider.value = SettingsManager.Instance.Settings.musicVolume;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = SettingsManager.Instance.Settings.sfxVolume;
    }

    public IEnumerator SelectFirst(GameObject target)
    {
        yield return new WaitForSecondsRealtime(0.02f);
        EventSystem.current.SetSelectedGameObject(target);
    }
 
    public void Volume()
    {
        volumeOptions = true;
        displayOptions = false;
        controls = false;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        volumeMenu.SetActive(true);
 
        StartCoroutine(SelectFirst(_volumeMenuFirst));
 
        SetMenuTimeScale();
    }

    public void Display()
    {
        displayOptions = true;
        volumeOptions = false;
        controls = false;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        displayMenu.SetActive(true);
 
        StartCoroutine(SelectFirst(_displayMenuFirst));
 
        SetMenuTimeScale();
    }

    public void Controls()
    {
        controls = true;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(true);
 
        StartCoroutine(SelectFirst(_controlsMenuFirst));
 
        SetMenuTimeScale();
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
 
        Brand[] brandPerColumn = { Brand.None, Brand.None, Brand.DemonX, Brand.BigStox, Brand.Killeez, Brand.VWave };
 
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
            else if (i == 1)
            {
                foreach (SpellData spell in SpellDictionary.Instance.spellList)
                {
                    if (spell != null && spell.spellType == SpellType.Universal)
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
 
        StartCoroutine(SelectFirst(_spellsMenuFirst));
        
 
        SetMenuTimeScale();
    }
 
    private int listScrollOffset = 0;
 
    public void SpellGlossaryNewTab()
    {
        DOTween.Kill(spellSelectedBorderTransform);
        
        spellListParent.transform.position = new Vector3(
            spellListParent.transform.position.x,
            spellListInitialY,
            spellListParent.transform.position.z
        );
        
        selectedSpell = 0;
        listScrollOffset = 0;
        
        spellSelectedBorderTransform.anchoredPosition = new Vector2(spellSelectedBorderTransform.anchoredPosition.x, 280f);
        spellListParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(spellSelectedBorderTransform.anchoredPosition.x, 280f);

        SpellSelectBorderAnimation(spellSelectedBorderTransform, 3f);
        
        int j = 0;
        spellTabList.Clear();
        
        for (int i = 0; i < spellGlossaryList.Length; i++)
        {
            if (j >= grid[tab].spells.Length)
            {
                spellGlossaryList[i].SetActive(false);
                continue;
            }
            
            if (spellGlossaryList[i].GetComponentInChildren<TextMeshProUGUI>().text == grid[tab].spells[j].spellName)
            {
                spellTabList.Add(spellGlossaryList[i]);
                spellGlossaryList[i].SetActive(true);
                RectTransform rt = spellGlossaryList[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(j * 80f));
                if (spellTabList[j].GetComponent<RectTransform>().anchoredPosition.y < -(4 * 80f)) spellTabList[j].SetActive(false);
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
        if ((one == -1 && ((spellSelectedBorderTransform.anchoredPosition.y > -440 && selectedSpell < spellTabList.Count) || selectedSpell >= spellTabList.Count - 1))
        || (one == 1 && ((spellSelectedBorderTransform.anchoredPosition.y < 40 && selectedSpell > 0) || selectedSpell <= 0))
        )
        {
            // Derive target deterministically from logical state — never accumulates float error
            float targetY = 280f - (selectedSpell - listScrollOffset) * 240f;

            DOTween.Kill(spellSelectedBorderTransform);
            spellSelectedBorderTransform
                .DOAnchorPos(new Vector2(spellSelectedBorderTransform.anchoredPosition.x, targetY), 0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true); // timeScale = 0 while paused — unscaled time required

            SpellSelectBorderAnimation(spellSelectedBorderTransform, 3f);

            for (int i = 0; i < spellTabList.Count; i++)
                spellTabList[i].SetActive(i >= listScrollOffset && i < listScrollOffset + 5);
        }
        else
        {
            if (one == -1) listScrollOffset++;
            else listScrollOffset--;

            RectTransform listRT = spellListParent.GetComponent<RectTransform>();
            Vector2 targetListPos = listRT.anchoredPosition + new Vector2(0, -one * 240f);
            DOTween.Kill(listRT);
            listRT.DOAnchorPos(targetListPos, 0.12f).SetEase(Ease.OutQuad).SetUpdate(true);

            SpellSelectBorderAnimation(spellSelectedBorderTransform, 3.2f);

            // Apply immediately — children move with the parent, so no pop/flicker
            for (int i = 0; i < spellTabList.Count; i++)
                spellTabList[i].SetActive(i >= listScrollOffset && i < listScrollOffset + 5);
        }
    }
 
    void ActivateOnly(int index)
    {
        for (int i = 0; i < spellGlossaryPanel.Length; i++)
        {
            spellGlossaryPanel[i].SetActive(i == index);
        }
    }

    void SpellSelectBorderAnimation(RectTransform border, float scale)
    {
        border.localScale = new Vector3(0f, border.localScale.y, border.localScale.z);
        border
            .DOScaleX(scale, 0.15f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    void ChangeSpellPanelView(float descriptionPanelYPos, Vector2 gifDisplayPanelPos, Vector2 gifDisplayPanelScale, Vector2 gifTransformPos, Vector2 gifTransformScale, Vector2 colorLayer3Pos)
    {
        showDescription = !showDescription;

        descriptionPanel.DOAnchorPos(new Vector2(descriptionPanel.anchoredPosition.x, descriptionPanelYPos), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        gifDisplayPanel.DOAnchorPos(new Vector2(gifDisplayPanelPos.x, gifDisplayPanelPos.y), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        gifDisplayPanel.DOSizeDelta(new Vector2(gifDisplayPanelScale.x, gifDisplayPanelScale.y), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        gifTransform.DOAnchorPos(new Vector2(gifTransformPos.x, gifTransformPos.y), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        gifTransform.DOScale(new Vector2(gifTransformScale.x, gifTransformScale.y), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        colorLayer3.gameObject.GetComponent<RectTransform>().DOAnchorPos(new Vector2(colorLayer3Pos.x, colorLayer3Pos.y), 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void TriggerSelectedButton()
    {
        GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
        if (selectedObject == null) return;

        Button selectedButton = selectedObject.GetComponent<Button>();
        if (selectedButton != null && selectedButton.interactable)
        {
            selectedButton.onClick.Invoke();
            StartCoroutine(SuppressSelectionForOneFrame());
        }

        if(selectedObject.name == "Back")
        {
            //play the positive select sfx
            SFX_Manager.Instance.PlayMenuSound("Negative Select");
        }
        else
        {
            //play the positive select sfx
            SFX_Manager.Instance.PlayMenuSound("Positive Select");
        }
    }

    private System.Collections.IEnumerator SuppressSelectionForOneFrame()
    {
        var current = EventSystem.current.currentSelectedGameObject;
        EventSystem.current.SetSelectedGameObject(null);
        yield return null;
        if (EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(current);
    }
 
    public void ReturnToLobby()
    {
        Resume();
        sceneUiManager.MainMenu();
        LoadSettings();
    }
 
    public void QuitGame()
    {
        DataManager.Instance.SaveToFile();
        Debug.Log("Quitting Spell Code SlingerZ");
        Application.Quit();
    }

    public void MasterVolume()
    {
        float volume = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        ApplyMasterMixerVolume(volume);

        if(SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(volume);
        }
    }
 
    public void MusicVolume()
    {
        float volume = musicVolumeSlider != null ? musicVolumeSlider.value : 1f;
        ApplyMusicMixerVolume(volume);

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(volume);
        }
    }
 
    public void SFXVolume()
    {
        float sfx_volume = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;
        ApplySfxMixerVolume(sfx_volume);

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetSfxVolume(sfx_volume);
        }
    }
 
    public void ToggleCameraShake()
    {
        screenShake = screenShakeToggle != null ? screenShakeToggle.isOn : !screenShake;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetScreenshake(screenShake);
        }
    }
 
    public void ToggleDynamicCamera()
    {
        if (IsDynamicCameraForcedScene())
        {
            dynamicCameraOverride = true;
            if (dynamicCameraToggle != null)
            {
                dynamicCameraToggle.SetIsOnWithoutNotify(true);
                dynamicCameraToggle.interactable = false;
            }
            return;
        }

        dynamicCameraOverride = dynamicCameraToggle != null ? dynamicCameraToggle.isOn : !dynamicCameraOverride;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetDynamicCamera(dynamicCameraOverride);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshDynamicCameraOptionForScene();
    }

    private bool IsDynamicCameraForcedScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == "Tutorial" || sceneName == "TrainingGrounds";
    }

    private void RefreshDynamicCameraOptionForScene()
    {
        bool forcedOn = IsDynamicCameraForcedScene();

        if (forcedOn)
        {
            dynamicCameraOverride = true;
        }
        else if (SettingsManager.Instance != null && SettingsManager.Instance.Settings != null)
        {
            dynamicCameraOverride = SettingsManager.Instance.Settings.dynamicCamera;
        }

        if (dynamicCameraToggle != null)
        {
            dynamicCameraToggle.SetIsOnWithoutNotify(dynamicCameraOverride);
            dynamicCameraToggle.interactable = !forcedOn;
        }
    }

    private void ApplyMasterMixerVolume(float volume)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = volume;
        }

        if (masterAudioMixer != null)
        {
            masterAudioMixer.SetFloat("MasterVolume", VolumeToDecibels(volume));
        }
    }

    private void ApplyMusicMixerVolume(float volume)
    {
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = volume;
        }

        if (musicAudioMixer != null)
        {
            musicAudioMixer.SetFloat("MusicVolume", VolumeToDecibels(volume));
        }
    }

    private void ApplySfxMixerVolume(float volume)
    {
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = volume;
        }

        if (sfxAudioMixer != null)
        {
            sfxAudioMixer.SetFloat("SFXVolume", VolumeToDecibels(volume));
        }

        if (menuSfxAudioMixer != null)
        {
            menuSfxAudioMixer.SetFloat("MenuSFXVolume", VolumeToDecibels(volume));
        }
    }

    private static float VolumeToDecibels(float volume)
    {
        return Mathf.Log10(Mathf.Max(MinMixerVolume, volume)) * 20f;
    }

    private void SetMenuTimeScale()
    {
        Time.timeScale = IsOnlineMatchActive() ? 1f : 0f;
    }

    private bool IsOnlineMatchActive()
    {
        GameManager manager = gameManager != null ? gameManager : GameManager.Instance;
        return manager != null && manager.isOnlineMatchActive;
    }

    private void ScopeUiInputToPausePlayer()
    {
        if (!paused)
        {
            RestoreUiInputDevices();
            return;
        }

        InputSystemUIInputModule module = GetUiInputModule();
        InputActionAsset actionsAsset = module != null ? module.actionsAsset : null;
        if (actionsAsset == null || !TryGetPausePlayerDevices(out InputDevice[] devices))
        {
            return;
        }

        if (!uiInputDevicesScoped || scopedUiActionsAsset != actionsAsset)
        {
            RestoreUiInputDevices();
            previousUiInputDevices = actionsAsset.devices;
            scopedUiActionsAsset = actionsAsset;
            uiInputDevicesScoped = true;
        }

        actionsAsset.devices = new ReadOnlyArray<InputDevice>(devices);
    }

    private void RestoreUiInputDevices()
    {
        if (!uiInputDevicesScoped)
        {
            return;
        }

        if (scopedUiActionsAsset != null)
        {
            scopedUiActionsAsset.devices = previousUiInputDevices;
        }

        scopedUiActionsAsset = null;
        previousUiInputDevices = null;
        uiInputDevicesScoped = false;
    }

    private InputSystemUIInputModule GetUiInputModule()
    {
        if (uiInputModule != null)
        {
            return uiInputModule;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        uiInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        return uiInputModule;
    }

    private bool TryGetPausePlayerDevices(out InputDevice[] devices)
    {
        devices = null;

        PlayerController player = GetPausePlayer();
        if (player == null)
        {
            return false;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.devices.Count > 0)
        {
            devices = CopyValidDevices(playerInput.devices);
            if (devices.Length > 0)
            {
                return true;
            }
        }

        if (player.inputs != null && player.inputs.PlayerActionMap.devices.HasValue)
        {
            devices = CopyValidDevices(player.inputs.PlayerActionMap.devices.Value);
            if (devices.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private InputDevice[] CopyValidDevices(ReadOnlyArray<InputDevice> sourceDevices)
    {
        List<InputDevice> devices = new List<InputDevice>();

        for (int i = 0; i < sourceDevices.Count; i++)
        {
            InputDevice device = sourceDevices[i];
            if (device != null && InputDeviceManager.IsValidInput(device) && !devices.Contains(device))
            {
                devices.Add(device);
            }
        }

        return devices.ToArray();
    }

    private Vector2 GetPausePlayerNavigation()
    {
        if (!paused)
        {
            return Vector2.zero;
        }

        Vector2 nav = Vector2.zero;

        if (ReadPausePlayerActionValue("Left") > 0.33f)
        {
            nav.x -= 1f;
        }

        if (ReadPausePlayerActionValue("Right") > 0.33f)
        {
            nav.x += 1f;
        }

        if (ReadPausePlayerActionValue("Down") > 0.33f)
        {
            nav.y -= 1f;
        }

        if (ReadPausePlayerActionValue("Up") > 0.33f)
        {
            nav.y += 1f;
        }

        return nav;
    }

    private float ReadPausePlayerActionValue(string actionName)
    {
        InputAction action = FindPausePlayerAction(actionName);
        return action != null ? action.ReadValue<float>() : 0f;
    }

    private bool WasPausePlayerSubmitPressedThisFrame()
    {
        return WasPausePlayerActionPressedThisFrame("Jump");
    }

    private bool WasPausePlayerBackPressedThisFrame()
    {
        return WasPausePlayerActionPressedThisFrame("Code");
    }

    private bool WasPausePlayerCancelPressedThisFrame()
    {
        return WasPausePlayerActionPressedThisFrame("Pause");
    }

    private bool WasPausePlayerActionPressedThisFrame(string actionName)
    {
        InputAction action = FindPausePlayerAction(actionName);
        return action != null && action.WasPressedThisFrame();
    }

    private InputAction FindPausePlayerAction(string actionName)
    {
        PlayerController player = GetPausePlayer();
        if (player == null)
        {
            return null;
        }

        InputPlayerBindings bindings = player.inputs;
        if (bindings != null && bindings.PlayerActionMap != null)
        {
            InputAction action = bindings.PlayerActionMap.FindAction(actionName, false);
            if (action != null)
            {
                return action;
            }
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.actions != null)
        {
            InputAction action = playerInput.actions.FindAction($"Gameplay/{actionName}", false);
            if (action != null)
            {
                return action;
            }
        }

        return player.inputs.PlayerActionMap != null ? player.inputs.PlayerActionMap.FindAction($"Gameplay/{actionName}", false) : null;
    }

    private PlayerController GetPausePlayer()
    {
        GameManager manager = gameManager != null ? gameManager : GameManager.Instance;
        if (manager == null || manager.players == null || playerPauseIndex < 0 || playerPauseIndex >= manager.players.Length)
        {
            return null;
        }

        return manager.players[playerPauseIndex];
    }

    private bool TryGetOnlineControlOptions(PlayerController player, out PlayerControlOptionsData options)
    {
        options = null;
        return IsOnlineMatchActive()
            && SettingsManager.Instance != null
            && SettingsManager.Instance.TryGetControlOptionsForPlayer(player, out options);
    }

    private bool GetRelativeInputOption()
    {
        PlayerController player = GetPausePlayer();
        if (player == null) return false;
        return TryGetOnlineControlOptions(player, out PlayerControlOptionsData options) ? options.relativeInputs : player.relativeInputs;
    }

    private bool GetToggleCodeInputOption()
    {
        PlayerController player = GetPausePlayer();
        if (player == null) return false;
        return TryGetOnlineControlOptions(player, out PlayerControlOptionsData options) ? options.toggleCodeInput : player.toggleCodeInput;
    }

    private bool GetTapJumpOption()
    {
        PlayerController player = GetPausePlayer();
        if (player == null) return false;
        return TryGetOnlineControlOptions(player, out PlayerControlOptionsData options) ? options.tapJump : player.tapJump;
    }

    private bool GetVibeCodingOption()
    {
        PlayerController player = GetPausePlayer();
        if (player == null) return false;
        return TryGetOnlineControlOptions(player, out PlayerControlOptionsData options) ? options.vibeCoding : player.vibeCoding;
    }

    private bool GetDownJumpSlideOption()
    {
        PlayerController player = GetPausePlayer();
        if (player == null) return false;
        return TryGetOnlineControlOptions(player, out PlayerControlOptionsData options) ? options.downJumpSlide : player.downJumpSlide;
    }

    private void SetPauseControlOptions(bool relativeInputs, bool toggleCodeInput, bool tapJump, bool vibeCoding, bool downJumpSlide)
    {
        PlayerController player = GetPausePlayer();
        if (player == null)
        {
            return;
        }

        if (IsOnlineMatchActive())
        {
            SettingsManager.Instance?.SaveControlOptionsForPlayer(
                player,
                relativeInputs,
                toggleCodeInput,
                tapJump,
                vibeCoding,
                downJumpSlide);
            return;
        }

        player.relativeInputs = relativeInputs;
        player.toggleCodeInput = toggleCodeInput;
        player.tapJump = tapJump;
        player.vibeCoding = vibeCoding;
        player.downJumpSlide = downJumpSlide;
        SettingsManager.Instance?.SaveControlOptionsForPlayer(player);
    }

    public void ResetPlayerControlsToDefaults()
    {
        CancelActiveRebind();
        ResetPausePlayerBindingOverrides();
        SetPauseControlOptions(false, false, false, false, false);
        RefreshControlToggleGraphics();
        RefreshControlGlyphs();
    }

    public void RebindSelectedControlButton()
    {
        TextSetter textSetter = GetCurrentSelectedTextSetter();
        InputAction targetAction = textSetter != null ? textSetter.TargetAction : null;
        if (targetAction == null)
        {
            Debug.LogWarning("Cannot rebind because the selected button does not have a child TextSetter with a target action.");
            return;
        }

        RebindControlButton(targetAction.name, textSetter);
    }

    public void RebindControlButton(string actionName)
    {
        RebindControlButton(actionName, GetCurrentSelectedTextSetter());
    }

    public void RebindJumpButton()
    {
        RebindControlButton("Jump");
    }

    private void RebindControlButton(string actionName, TextSetter textSetter)
    {
        PlayerController player = GetPausePlayer();
        InputDevice inputDevice = FindPausePlayerInputDevice(player);
        InputAction action = string.IsNullOrEmpty(actionName) ? null : FindPausePlayerAction(actionName);

        if (player == null || inputDevice == null || action == null)
        {
            Debug.LogWarning($"Cannot rebind {actionName} because the pause player, input device, or action was not found.");
            return;
        }

        int bindingIndex = FindBindingIndexForDevice(action, inputDevice);
        if (bindingIndex < 0)
        {
            Debug.LogWarning($"Cannot rebind {action.name} because no binding exists for {inputDevice.displayName}.");
            return;
        }

        StartControlRebind(player, inputDevice, action, bindingIndex, textSetter);
    }

    private void StartControlRebind(
        PlayerController player,
        InputDevice inputDevice,
        InputAction action,
        int bindingIndex,
        TextSetter textSetter)
    {
        CancelActiveRebind();
        textSetter?.ClearGlyph();

        activeRebindAction = action;
        activeRebindActionWasEnabled = action.enabled;
        if (activeRebindActionWasEnabled)
        {
            action.Disable();
        }

        activeRebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .OnPotentialMatch(operation => CompleteValidRebindMatch(operation, inputDevice))
            .OnComplete(operation => FinishControlRebind(player, textSetter))
            .OnCancel(operation => FinishControlRebind(player, textSetter));

        for (int i = 0; i < BannedRebindInputs.Length; i++)
        {
            activeRebindOperation.WithControlsExcluding(BannedRebindInputs[i]);
        }

        activeRebindOperation.Start();
    }

    private void CompleteValidRebindMatch(InputActionRebindingExtensions.RebindingOperation operation, InputDevice inputDevice)
    {
        InputControl selectedControl = operation.selectedControl;
        if (selectedControl == null)
        {
            return;
        }

        if (selectedControl.device != inputDevice || IsBannedRebindInput(selectedControl))
        {
            operation.RemoveCandidate(selectedControl);
            return;
        }

        operation.Complete();
    }

    private void FinishControlRebind(PlayerController player, TextSetter textSetter)
    {
        RestoreActiveRebindAction();
        DisposeActiveRebindOperation();

        SettingsManager.Instance?.SaveControlOptionsForPlayer(player);
        textSetter?.UpdateGlyph();
    }

    private void CancelActiveRebind()
    {
        if (activeRebindOperation != null && activeRebindOperation.started && !activeRebindOperation.completed && !activeRebindOperation.canceled)
        {
            activeRebindOperation.Cancel();
            return;
        }

        RestoreActiveRebindAction();
        DisposeActiveRebindOperation();
    }

    private void RestoreActiveRebindAction()
    {
        if (activeRebindAction != null && activeRebindActionWasEnabled)
        {
            activeRebindAction.Enable();
        }

        activeRebindAction = null;
        activeRebindActionWasEnabled = false;
    }

    private void DisposeActiveRebindOperation()
    {
        activeRebindOperation?.Dispose();
        activeRebindOperation = null;
    }

    private bool IsBannedRebindInput(InputControl inputControl)
    {
        if (inputControl == null)
        {
            return true;
        }

        for (int i = 0; i < BannedRebindInputs.Length; i++)
        {
            if (InputControlPath.Matches(BannedRebindInputs[i], inputControl))
            {
                return true;
            }
        }

        return false;
    }

    private InputDevice FindPausePlayerInputDevice(PlayerController player)
    {
        if (player == null)
        {
            return null;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.devices.Count > 0)
        {
            return playerInput.devices[0];
        }

        try
        {
            return player.inputs != null ? player.inputs.InputDevice : null;
        }
        catch
        {
            return null;
        }
    }

    private int FindBindingIndexForDevice(InputAction action, InputDevice inputDevice)
    {
        if (action == null || inputDevice == null)
        {
            return -1;
        }

        string devicePath = GetBindingDevicePath(inputDevice);
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath))
            {
                continue;
            }

            if (binding.effectivePath.StartsWith(devicePath, System.StringComparison.OrdinalIgnoreCase)
                || InputControlPath.Matches(binding.effectivePath, inputDevice))
            {
                return i;
            }
        }

        return -1;
    }

    private string GetBindingDevicePath(InputDevice inputDevice)
    {
        if (inputDevice is Keyboard)
        {
            return "<Keyboard>";
        }

        if (inputDevice is Gamepad)
        {
            return "<Gamepad>";
        }

        if (inputDevice is Joystick)
        {
            return "<Joystick>";
        }

        return $"<{inputDevice.layout}>";
    }

    private TextSetter GetCurrentSelectedTextSetter()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null ? selected.GetComponentInChildren<TextSetter>() : null;
    }

    private void ResetPausePlayerBindingOverrides()
    {
        InputActionMap actionMap = FindPausePlayerActionMap();
        actionMap?.RemoveAllBindingOverrides();
    }

    private void RefreshControlGlyphs()
    {
        if (controlsMenu == null)
        {
            return;
        }

        TextSetter[] textSetters = controlsMenu.GetComponentsInChildren<TextSetter>(true);
        for (int i = 0; i < textSetters.Length; i++)
        {
            textSetters[i]?.UpdateGlyph();
        }
    }

    private InputActionMap FindPausePlayerActionMap()
    {
        PlayerController player = GetPausePlayer();
        if (player == null)
        {
            return null;
        }

        InputPlayerBindings bindings = player.inputs;
        if (bindings != null && bindings.PlayerActionMap != null)
        {
            return bindings.PlayerActionMap;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        return playerInput != null ? playerInput.currentActionMap : null;
    }

    private void RefreshControlToggleGraphics()
    {
        if (relativeInputToggleGraphic != null) relativeInputToggleGraphic.SetIsOnWithoutNotify(UIRelativeInput);
        if (codeInputToggleGraphic != null) codeInputToggleGraphic.SetIsOnWithoutNotify(UIToggleCodeInput);
        if (tapJumpToggleGraphic != null) tapJumpToggleGraphic.SetIsOnWithoutNotify(UITapJump);
        if (vibeCodingToggleGraphic != null) vibeCodingToggleGraphic.SetIsOnWithoutNotify(UIVibeCode);
        if (downJumpSlideToggleGraphic != null) downJumpSlideToggleGraphic.SetIsOnWithoutNotify(UIDownJumpSlide);
    }

    private bool GetToggleValue(Toggle toggle, bool currentValue)
    {
        return toggle != null ? toggle.isOn : !currentValue;
    }
 
    public void ToggleRelativeInput()
    {
        UIRelativeInput = GetToggleValue(relativeInputToggleGraphic, UIRelativeInput);
        if (relativeInputToggleGraphic != null) relativeInputToggleGraphic.SetIsOnWithoutNotify(UIRelativeInput);
    }

    public void ToggleCodeInput()
    {
        UIToggleCodeInput = GetToggleValue(codeInputToggleGraphic, UIToggleCodeInput);
        if (codeInputToggleGraphic != null) codeInputToggleGraphic.SetIsOnWithoutNotify(UIToggleCodeInput);
    }

    public void ToggleTapJump()
    {
        UITapJump = GetToggleValue(tapJumpToggleGraphic, UITapJump);
        if (tapJumpToggleGraphic != null) tapJumpToggleGraphic.SetIsOnWithoutNotify(UITapJump);
    }

    public void ToggleVibeCoding()
    {
        UIVibeCode = GetToggleValue(vibeCodingToggleGraphic, UIVibeCode);
        if (vibeCodingToggleGraphic != null) vibeCodingToggleGraphic.SetIsOnWithoutNotify(UIVibeCode);
    }

    public void ToggleDownJumpSlide()
    {
        UIDownJumpSlide = GetToggleValue(downJumpSlideToggleGraphic, UIDownJumpSlide);
        if (downJumpSlideToggleGraphic != null) downJumpSlideToggleGraphic.SetIsOnWithoutNotify(UIDownJumpSlide);
    }
}

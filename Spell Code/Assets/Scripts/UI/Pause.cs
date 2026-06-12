using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Audio;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using YamlDotNet.Serialization;
using GifImporter; 

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
    public bool screenShake = true;
    public bool dynamicCameraOverride = true;
    public Toggle screenShakeToggle;
    public Toggle dynamicCameraToggle;
    private SceneUiManager sceneUiManager;
    private const float MinMixerVolume = 0.0001f;
 
    public GameObject _pauseMenuFirst;
    public GameObject _optionsMenuFirst;
    public GameObject _controlsMenuFirst;
    public GameObject _spellsMenuFirst;
 
    public TextMeshProUGUI playerPausedText;
    public Toggle relativeInputToggleGraphic;
    public Toggle codeInputToggleGraphic;
    public Toggle tapJumpToggleGraphic;
    public Toggle vibeCodingToggleGraphic;

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
    public GameObject unselectedSpell;
    public GameObject spellListParent;
    public GameObject[] spellGlossaryList;
    public List<GameObject> spellTabList = new List<GameObject>();
    public Image colorLayer;
    public Image colorLayer2;
    public Image colorLayer3;
    public GifPlayer gifPlayer;
    public Sprite[] fellas;
    public GameObject fella;
 
    private int tab = 0;
    private int selectedSpell;
    private float spellListInitialY;
    private int openedFrame = -1;
 
    // Cooldown to prevent held-stick from firing every frame
    private float navCooldown = 0f;
    private const float NAV_COOLDOWN_TIME = 0.2f;
    // Track last frame's nav value to detect fresh presses
    private Vector2 lastNavValue = Vector2.zero;
 
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

    public bool UIVibeCode
    {
        get { return gameManager.players[playerPauseIndex].vibeCoding; }
        set 
        {
            gameManager.players[playerPauseIndex].vibeCoding = value; 
        }
    }
 
    private InputSystem_Actions input;
 
    void OnEnable()  { input.Enable(); }
    void OnDisable() { input.Disable(); }
 
    void Awake()
    {
        input = new InputSystem_Actions();
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
    }
 
    void Update()
    {
        if (spells)
        {
            SpellGlossaryNavigation();
            UpdateSpellDisplay();
        }
 
        if (paused && Time.frameCount != openedFrame && input.UI.Cancel.WasPressedThisFrame())
        {
            Resume();
        }

        if (input.UI.Back.WasPressedThisFrame() && !controls && paused && Time.frameCount != openedFrame)
        {
            Pausing();
        }
    }
 
    private void SpellGlossaryNavigation()
    {
        Vector2 nav = input.UI.Navigate.ReadValue<Vector2>();
 
        // Tick down cooldown using unscaled time so it works while paused (timeScale = 0)
        navCooldown -= Time.unscaledDeltaTime;
 
        // Only act on a fresh directional press OR if cooldown has expired while stick is held
        bool freshPress = (nav != Vector2.zero && lastNavValue == Vector2.zero);
        bool heldAndReady = (nav != Vector2.zero && navCooldown <= 0f);
 
        if (freshPress || heldAndReady)
        {
            navCooldown = NAV_COOLDOWN_TIME;
 
            if (nav.y > 0 && selectedSpell > 0)
            {
                selectedSpell--;
                SpellGlossaryListSelection(1);
            }
            else if (nav.y < 0 && selectedSpell < grid[tab].spells.Length - 1)
            {
                selectedSpell++;
                SpellGlossaryListSelection(-1);
            }
 
            if (nav.x < 0)
            {
                tab = (tab == 0) ? 5 : tab - 1;
                SpellGlossaryNewTab();
                SpellSelectBorderAnimation(spellGlossaryPanel[tab].GetComponent<RectTransform>(), 1f);
            }
            else if (nav.x > 0)
            {
                tab = (tab == 5) ? 0 : tab + 1;
                SpellGlossaryNewTab();
                SpellSelectBorderAnimation(spellGlossaryPanel[tab].GetComponent<RectTransform>(), 1f);
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
                        break;
                    case Brand.BigStox:
                        spellSelectedBorder.color = GameManager.colors["blue"];
                        colorLayer.color = GameManager.colors["blue"];
                        colorLayer2.color = GameManager.colors["blue"];
                        colorLayer3.color = GameManager.colors["blue"];
                        break;
                    case Brand.DemonX:
                        spellSelectedBorder.color = GameManager.colors["red"];
                        colorLayer.color = GameManager.colors["red"];
                        colorLayer2.color = GameManager.colors["red"];
                        colorLayer3.color = GameManager.colors["red"];
                        break;
                    case Brand.Killeez:
                        spellSelectedBorder.color = GameManager.colors["yellow"];
                        colorLayer.color = GameManager.colors["yellow"];
                        colorLayer2.color = GameManager.colors["yellow"];
                        colorLayer3.color = GameManager.colors["yellow"];
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
        paused = false;
        options = false;
        controls = false;
        spells = false;
        openedFrame = -1;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        darkPanel.SetActive(false);
        spellsMenu.SetActive(false);
 
        EventSystem.current.SetSelectedGameObject(null);
        SaveSettings();
        Time.timeScale = 1f;    
    }

    public void SaveSettings()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings == null)
        {
            return;
        }

        settings.SetDynamicCamera(dynamicCameraOverride);
        settings.SetScreenshake(screenShake);
        settings.SetFullscreen(true);
        if (musicVolumeSlider != null) settings.SetMusicVolume(musicVolumeSlider.value);
        if (sfxVolumeSlider != null) settings.SetSfxVolume(sfxVolumeSlider.value);
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
        if (dynamicCameraToggle != null) dynamicCameraToggle.SetIsOnWithoutNotify(dynamicCameraOverride);
        if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(screenShake);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
        ApplyMusicMixerVolume(settings.musicVolume);
        ApplySfxMixerVolume(settings.sfxVolume);
    }
 
    public void Pausing()
    {
        paused = true;
        options = false;
        controls = false;
        spells = false;
        openedFrame = Time.frameCount;
        pausemenu.SetActive(true);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(false);
        spellsMenu.SetActive(false);
        darkPanel.SetActive(true);

        playerPausedText.text = "P" + (playerPauseIndex + 1) + (IsOnlineMatchActive() ? " Menu" : " Paused");
 
        relativeInputToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].relativeInputs);
        codeInputToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].toggleCodeInput);
        tapJumpToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].tapJump);
        vibeCodingToggleGraphic.SetIsOnWithoutNotify(gameManager.players[playerPauseIndex].vibeCoding);
 
        EventSystem.current.SetSelectedGameObject(_pauseMenuFirst);
 
        SetMenuTimeScale();
    }
 
    public void Options()
    {
        options = true;
        controls = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(true);
        controlsMenu.SetActive(false);
 
        EventSystem.current.SetSelectedGameObject(_optionsMenuFirst);
 
        SetMenuTimeScale();
    }
 
    public void Controls()
    {
        controls = true;
        options = false;
        pausemenu.SetActive(false);
        optionsMenu.SetActive(false);
        controlsMenu.SetActive(true);
 
        EventSystem.current.SetSelectedGameObject(_controlsMenuFirst);
 
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
 
        EventSystem.current.SetSelectedGameObject(_spellsMenuFirst);
 
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

            SpellSelectBorderAnimation(spellSelectedBorderTransform, 3f);

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
 
    public void ReturnToLobby()
    {
        Resume();
        sceneUiManager.MainMenu();
    }
 
    public void QuitGame()
    {
        DataManager.Instance.SaveToFile();
        Debug.Log("Quitting Spell Code SlingerZ");
        Application.Quit();
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
        float volume = sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f;
        ApplySfxMixerVolume(volume);

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetSfxVolume(volume);
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
        dynamicCameraOverride = dynamicCameraToggle != null ? dynamicCameraToggle.isOn : !dynamicCameraOverride;

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetDynamicCamera(dynamicCameraOverride);
        }
    }

    private void ApplyMusicMixerVolume(float volume)
    {
        if (musicAudioMixer != null)
        {
            musicAudioMixer.SetFloat("MusicVolume", VolumeToDecibels(volume));
        }
    }

    private void ApplySfxMixerVolume(float volume)
    {
        AudioMixer mixer = sfxAudioMixer != null ? sfxAudioMixer : musicAudioMixer;
        if (mixer != null)
        {
            mixer.SetFloat("SFXVolume", VolumeToDecibels(volume));
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

    public void ToggleVibeCoding()
    {
        UIVibeCode = !UIVibeCode;
    }
}

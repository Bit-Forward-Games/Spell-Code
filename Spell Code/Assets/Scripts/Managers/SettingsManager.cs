using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class GameSettingsData
{
    public int version = 1;

    public bool firstLaunchComplete = false;

    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;

    public bool fullscreen = true;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public bool dynamicCamera = true;
    public bool screenshake = true;
}

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string SettingsFileName = "settings.json";
    private const string ControlOptionsFileName = "control_options_session.json";

    public GameSettingsData Settings { get; private set; }
    public ControlOptionsSessionData ControlOptions { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, SettingsFileName);
    private string ControlOptionsSavePath => Path.Combine(Application.persistentDataPath, ControlOptionsFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject settingsObject = new GameObject("SettingsManager");
        settingsObject.AddComponent<SettingsManager>();
        DontDestroyOnLoad(settingsObject);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        LoadControlOptions();
        ApplySettings();
    }

    private void OnApplicationQuit()
    {
        DeleteControlOptionsSave();
    }

    public bool IsFirstLaunch()
    {
        return Settings == null || !Settings.firstLaunchComplete;
    }

    public void MarkFirstLaunchComplete()
    {
        Settings.firstLaunchComplete = true;
        Save();
    }

    public void SetMasterVolume(float volume)
    {
        Settings.masterVolume = volume;
        Save();
    }

    public void SetMusicVolume(float volume)
    {
        Settings.musicVolume = volume;
        Save();
    }

    public void SetSfxVolume(float volume)
    {
        Settings.sfxVolume = volume;
        Save();
    }

    public void SetFullscreen(bool fullscreen)
    {
        Settings.fullscreen = fullscreen;
        ApplyDisplaySettings();
        Save();
    }

    // public void SetResolution(int width, int height)
    // {
    //     Settings.resolutionWidth = Mathf.Max(1, width);
    //     Settings.resolutionHeight = Mathf.Max(1, height);
    //     ApplyDisplaySettings();
    //     Save();
    // }



    public void SetScreenshake(bool enabled)
    {
        Settings.screenshake = enabled;
        Save();
    }

    public void SetDynamicCamera(bool enabled)
    {
        Settings.dynamicCamera = enabled;
        Save();
    }

    public void ApplySettings()
    {
        //ApplyAudioSettings();
        ApplyDisplaySettings();
    }

    //public void ApplyAudioSettings()
    //{
    //    //AudioListener.volume = Mathf.Clamp01(Settings.masterVolume);
    //}

    public void ApplyDisplaySettings()
    {
        Screen.SetResolution(
            Mathf.Max(1, Settings.resolutionWidth),
            Mathf.Max(1, Settings.resolutionHeight),
            Settings.fullscreen
        );
    }


    public void Save()
    {
        if (Settings == null)
        {
            Settings = CreateDefaultSettings();
        }

        string json = JsonUtility.ToJson(Settings, true);
        File.WriteAllText(SavePath, json);
    }

    public void SaveControlOptionsForPlayer(PlayerController player)
    {
        if (!TryGetControllerId(player, out int controllerId))
        {
            return;
        }

        SaveControlOptionsForPlayer(
            player,
            player.relativeInputs,
            player.toggleCodeInput,
            player.tapJump,
            player.vibeCoding,
            player.downJumpSlide);
    }

    public void SaveControlOptionsForPlayer(
        PlayerController player,
        bool relativeInputs,
        bool toggleCodeInput,
        bool tapJump,
        bool vibeCoding,
        bool downJumpSlide)
    {
        if (!TryGetControllerId(player, out int controllerId))
        {
            return;
        }

        if (ControlOptions == null)
        {
            ControlOptions = CreateDefaultControlOptions();
        }

        PlayerControlOptionsData options = GetOrCreateControlOptions(controllerId);
        options.controllerId = controllerId;
        options.relativeInputs = relativeInputs;
        options.toggleCodeInput = toggleCodeInput;
        options.tapJump = tapJump;
        options.vibeCoding = vibeCoding;
        options.downJumpSlide = downJumpSlide;
        SaveInputBindingOverrides(player, options);

        SaveControlOptions();
    }

    public bool TryGetControlOptionsForPlayer(PlayerController player, out PlayerControlOptionsData options)
    {
        options = null;

        if (player == null || !TryGetControllerId(player, out int controllerId))
        {
            return false;
        }

        if (ControlOptions == null)
        {
            LoadControlOptions();
        }

        options = FindControlOptions(controllerId);
        return options != null;
    }

    public bool TryApplyControlOptionsForPlayer(PlayerController player)
    {
        if (player == null || !TryGetControllerId(player, out int controllerId))
        {
            return false;
        }

        if (ControlOptions == null)
        {
            LoadControlOptions();
        }

        PlayerControlOptionsData options = FindControlOptions(controllerId);
        if (options == null)
        {
            return false;
        }

        player.relativeInputs = options.relativeInputs;
        player.toggleCodeInput = options.toggleCodeInput;
        player.tapJump = options.tapJump;
        player.vibeCoding = options.vibeCoding;
        player.downJumpSlide = options.downJumpSlide;
        ApplyInputBindingOverrides(player, options);
        return true;
    }

    public void SaveControlOptions()
    {
        if (ControlOptions == null)
        {
            ControlOptions = CreateDefaultControlOptions();
        }

        string json = JsonUtility.ToJson(ControlOptions, true);
        File.WriteAllText(ControlOptionsSavePath, json);
    }

    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            Settings = CreateDefaultSettings();
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            Settings = JsonUtility.FromJson<GameSettingsData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load settings file. Creating new settings. Error: {exception.Message}");
            Settings = null;
        }

        if (Settings == null)
        {
            Settings = CreateDefaultSettings();
            Save();
        }
    }

    public void LoadControlOptions()
    {
        if (!File.Exists(ControlOptionsSavePath))
        {
            ControlOptions = CreateDefaultControlOptions();
            return;
        }

        try
        {
            string json = File.ReadAllText(ControlOptionsSavePath);
            ControlOptions = JsonUtility.FromJson<ControlOptionsSessionData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load temporary control options file. Creating new session options. Error: {exception.Message}");
            ControlOptions = null;
        }

        if (ControlOptions == null)
        {
            ControlOptions = CreateDefaultControlOptions();
        }
    }

    public void ResetToDefaults()
    {
        bool firstLaunchComplete = Settings != null && Settings.firstLaunchComplete;

        Settings = CreateDefaultSettings();
        Settings.firstLaunchComplete = firstLaunchComplete;

        ApplySettings();
        Save();
    }

    private GameSettingsData CreateDefaultSettings()
    {
        Resolution resolution = Screen.currentResolution;

        return new GameSettingsData
        {
            resolutionWidth = Mathf.Max(1, resolution.width),
            resolutionHeight = Mathf.Max(1, resolution.height),
            fullscreen = Screen.fullScreen,
        };
    }

    private ControlOptionsSessionData CreateDefaultControlOptions()
    {
        return new ControlOptionsSessionData();
    }

    private PlayerControlOptionsData GetOrCreateControlOptions(int controllerId)
    {
        PlayerControlOptionsData options = FindControlOptions(controllerId);
        if (options != null)
        {
            return options;
        }

        options = new PlayerControlOptionsData
        {
            controllerId = controllerId
        };
        ControlOptions.playerOptions.Add(options);
        return options;
    }

    private PlayerControlOptionsData FindControlOptions(int controllerId)
    {
        if (ControlOptions?.playerOptions == null)
        {
            return null;
        }

        for (int i = 0; i < ControlOptions.playerOptions.Count; i++)
        {
            PlayerControlOptionsData options = ControlOptions.playerOptions[i];
            if (options != null && options.controllerId == controllerId)
            {
                return options;
            }
        }

        return null;
    }

    private void SaveInputBindingOverrides(PlayerController player, PlayerControlOptionsData options)
    {
        if (options == null)
        {
            return;
        }

        InputActionMap actionMap = GetPlayerActionMap(player);
        if (actionMap == null)
        {
            return;
        }

        options.inputBindingsSaved = true;
        options.inputBindingOverridesJson = actionMap.SaveBindingOverridesAsJson();
    }

    private void ApplyInputBindingOverrides(PlayerController player, PlayerControlOptionsData options)
    {
        if (options == null || !options.inputBindingsSaved)
        {
            return;
        }

        InputActionMap actionMap = GetPlayerActionMap(player);
        if (actionMap == null)
        {
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(options.inputBindingOverridesJson))
            {
                actionMap.RemoveAllBindingOverrides();
            }
            else
            {
                actionMap.LoadBindingOverridesFromJson(options.inputBindingOverridesJson);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to apply saved input bindings. Error: {exception.Message}");
        }
    }

    private InputActionMap GetPlayerActionMap(PlayerController player)
    {
        if (player == null)
        {
            return null;
        }

        if (player.inputs != null && player.inputs.PlayerActionMap != null)
        {
            return player.inputs.PlayerActionMap;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            return playerInput.currentActionMap;
        }

        return null;
    }

    private bool TryGetControllerId(PlayerController player, out int controllerId)
    {
        controllerId = -1;

        if (player == null)
        {
            return false;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null && playerInput.devices.Count > 0 && playerInput.devices[0] != null)
        {
            controllerId = playerInput.devices[0].deviceId;
            return true;
        }

        InputDevice inputDevice = null;
        try
        {
            inputDevice = player.inputs != null ? player.inputs.InputDevice : null;
        }
        catch (Exception)
        {
            inputDevice = null;
        }

        if (inputDevice == null)
        {
            return false;
        }

        controllerId = inputDevice.deviceId;
        return true;
    }

    private void DeleteControlOptionsSave()
    {
        if (!File.Exists(ControlOptionsSavePath))
        {
            return;
        }

        try
        {
            File.Delete(ControlOptionsSavePath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to delete temporary control options file. Error: {exception.Message}");
        }
    }
}

[Serializable]
public class ControlOptionsSessionData
{
    public int version = 1;
    public List<PlayerControlOptionsData> playerOptions = new List<PlayerControlOptionsData>();
}

[Serializable]
public class PlayerControlOptionsData
{
    public int controllerId = -1;
    public bool relativeInputs = false;
    public bool toggleCodeInput = false;
    public bool tapJump = false;
    public bool vibeCoding = false;
    public bool downJumpSlide = false;
    public bool inputBindingsSaved = false;
    public string inputBindingOverridesJson = string.Empty;
}

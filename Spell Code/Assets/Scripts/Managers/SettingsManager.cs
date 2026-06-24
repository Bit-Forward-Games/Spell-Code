using System;
using System.IO;
using UnityEngine;

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

    public GameSettingsData Settings { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, SettingsFileName);

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
        ApplySettings();
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
        ApplyAudioSettings();
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
        ApplyAudioSettings();
        ApplyDisplaySettings();
    }

    public void ApplyAudioSettings()
    {
        //AudioListener.volume = Mathf.Clamp01(Settings.masterVolume);
    }

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
}

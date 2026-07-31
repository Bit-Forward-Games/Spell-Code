using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[Serializable]
public class GameSettingsData
{
    public int version = 2;

    public bool firstLaunchComplete = false;

    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;

    public bool fullscreen = true;
    public FullScreenMode displayMode = FullScreenMode.ExclusiveFullScreen;
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

    // Online input is shared across every local device, so PlayerInput.devices[0] is not a stable
    // profile identity. Preserve the device/profile used by the pre-online local player for the
    // lifetime of the match instead of letting network slot spawn order choose one.
    private int onlineLocalControllerId = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // ExecuteOrder66 (the return-to-menu reset) destroys every DontDestroyOnLoad object,
        // this manager included, and RuntimeInitializeOnLoadMethod only runs once per process.
        // Without re-creating it, ALL settings functionality silently no-ops for the rest of the
        // session after the first cold load — every caller null-checks Instance, so control
        // options (including their propagation into the online input stream), volumes and camera
        // prefs just stop saving/applying. Same re-create pattern as PlayerLogFileWriter; state
        // survives in settings.json / control_options_session.json, which Awake reloads.
        SceneManager.sceneLoaded -= OnSceneLoadedEnsureInstance;
        SceneManager.sceneLoaded += OnSceneLoadedEnsureInstance;
        EnsureInstance();
    }

    private static void OnSceneLoadedEnsureInstance(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
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

    private void OnDestroy()
    {
        // Clear the stale reference on teardown so `Instance != null` (and any `Instance?.`,
        // which bypasses Unity's destroyed-object null) can't act on a dead object before the
        // sceneLoaded re-create runs.
        if (Instance == this)
        {
            Instance = null;
        }
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
        SetDisplayMode(fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed);
    }

    public void SetDisplayMode(FullScreenMode displayMode)
    {
        Settings.displayMode = NormalizeDisplayMode(displayMode);
        // Retain this legacy field so version-1 settings and any older callers remain compatible.
        Settings.fullscreen = Settings.displayMode != FullScreenMode.Windowed;
        ApplyDisplaySettings();
        Save();
    }

    public void SetResolution(int width, int height)
    {
        Settings.resolutionWidth = Mathf.Max(1, width);
        Settings.resolutionHeight = Mathf.Max(1, height);
        ApplyDisplaySettings();
        Save();
    }



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
        int width = Mathf.Max(1, Settings.resolutionWidth);
        int height = Mathf.Max(1, Settings.resolutionHeight);
        Vector2Int maximumResolution = GetMaximum16By9Resolution();

        if (width > maximumResolution.x || height > maximumResolution.y)
        {
            width = maximumResolution.x;
            height = maximumResolution.y;
            Settings.resolutionWidth = width;
            Settings.resolutionHeight = height;
        }

        Screen.SetResolution(
            width,
            height,
            NormalizeDisplayMode(Settings.displayMode)
        );
    }

    public Vector2Int GetMaximum16By9Resolution()
    {
        Vector2Int displaySize = GetActiveDisplaySize();
        if (displaySize.x <= 0 || displaySize.y <= 0)
        {
            return new Vector2Int(
                Mathf.Max(1, Settings?.resolutionWidth ?? Screen.width),
                Mathf.Max(1, Settings?.resolutionHeight ?? Screen.height));
        }

        const float targetAspect = 16f / 9f;
        float displayAspect = (float)displaySize.x / displaySize.y;

        int width;
        int height;
        if (displayAspect >= targetAspect)
        {
            height = displaySize.y;
            width = Mathf.RoundToInt(height * targetAspect);
        }
        else
        {
            width = displaySize.x;
            height = Mathf.RoundToInt(width / targetAspect);
        }

        return new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
    }

    private static Vector2Int GetActiveDisplaySize()
    {
        DisplayInfo displayInfo = Screen.mainWindowDisplayInfo;
        if (displayInfo.width > 0 && displayInfo.height > 0)
        {
            return new Vector2Int(displayInfo.width, displayInfo.height);
        }

        Resolution currentResolution = Screen.currentResolution;
        if (currentResolution.width > 0 && currentResolution.height > 0)
        {
            return new Vector2Int(currentResolution.width, currentResolution.height);
        }

        if (Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0)
        {
            return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
        }

        return new Vector2Int(Screen.width, Screen.height);
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

    public void SaveInputBindingOverridesForPlayer(
        PlayerController player,
        InputDevice bindingDevice = null,
        string[] changedBindingIds = null)
    {
        if (player == null)
        {
            return;
        }

        bool hasProfileController = TryGetControllerId(player, out int profileControllerId);
        bool hasBindingController = bindingDevice != null
            && InputDeviceManager.IsValidInput(bindingDevice);
        if (!hasProfileController && !hasBindingController)
        {
            return;
        }

        if (ControlOptions == null)
        {
            ControlOptions = CreateDefaultControlOptions();
        }

        bool bindingsSaved = false;
        if (hasProfileController)
        {
            bindingsSaved = SaveInputBindingOverridesForController(player, profileControllerId);
        }

        // An online PlayerInput listens to every local device, while its control-option profile is
        // pinned to the device that entered the match. Also update the device that was actually
        // rebound so the same device can restore its override when it joins the SoloLobby again.
        // Merge only the bindings changed by this operation: the shared online map was initialized
        // from the pinned profile and must not replace unrelated overrides in another profile.
        if (hasBindingController && bindingDevice.deviceId != profileControllerId)
        {
            bool bindingDeviceSaved = MergeInputBindingOverridesForController(
                player,
                bindingDevice.deviceId,
                changedBindingIds);
            bindingsSaved = bindingDeviceSaved || bindingsSaved;
        }

        if (bindingsSaved)
        {
            SaveControlOptions();
        }
    }

    public void ResetControlOptionsForPlayer(PlayerController player, InputDevice bindingDevice = null)
    {
        if (player == null)
        {
            return;
        }

        bool hasProfileController = TryGetControllerId(player, out int profileControllerId);
        bool hasBindingController = bindingDevice != null
            && InputDeviceManager.IsValidInput(bindingDevice);
        if (!hasProfileController && !hasBindingController)
        {
            return;
        }

        if (ControlOptions == null)
        {
            ControlOptions = CreateDefaultControlOptions();
        }

        if (hasProfileController)
        {
            ResetControlOptionsForController(player, profileControllerId);
        }

        // Reset is intentionally broader than an individual rebind: both the binding map and the
        // five control toggles must be cleared for the device that will own the offline profile.
        if (hasBindingController && bindingDevice.deviceId != profileControllerId)
        {
            ResetControlOptionsForController(player, bindingDevice.deviceId);
        }

        SaveControlOptions();
    }

    public void EndOnlineLocalControlSession(PlayerController player)
    {
        // This must run while GameManager still identifies player as the online local player. It
        // captures the only live action-map clone before StopMatch destroys it, without replacing
        // control toggles that may still be delayed in rollback-simulated PlayerController fields.
        SaveInputBindingOverridesForPlayer(player);
        onlineLocalControllerId = -1;
    }

    public void BeginOnlineLocalControlSession(PlayerController sourcePlayer)
    {
        // StartOnlineMatch can be re-entered while applying a roster snapshot. In that case the
        // current online player is already using the correct cached profile, so retain it.
        if (onlineLocalControllerId >= 0 && IsOnlineLocalPlayer(sourcePlayer))
        {
            return;
        }

        onlineLocalControllerId = TryGetDirectControllerId(sourcePlayer, out int controllerId)
            ? controllerId
            : -1;
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
            return;
        }

        if (Settings.version < 2)
        {
            Settings.displayMode = Settings.fullscreen
                ? FullScreenMode.ExclusiveFullScreen
                : FullScreenMode.Windowed;
            Settings.version = 2;
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
        FullScreenMode displayMode = NormalizeDisplayMode(Screen.fullScreenMode);

        return new GameSettingsData
        {
            resolutionWidth = Mathf.Max(1, resolution.width),
            resolutionHeight = Mathf.Max(1, resolution.height),
            fullscreen = displayMode != FullScreenMode.Windowed,
            displayMode = displayMode,
        };
    }

    private static FullScreenMode NormalizeDisplayMode(FullScreenMode displayMode)
    {
        switch (displayMode)
        {
            case FullScreenMode.ExclusiveFullScreen:
            case FullScreenMode.FullScreenWindow:
            case FullScreenMode.Windowed:
                return displayMode;
            default:
                return FullScreenMode.FullScreenWindow;
        }
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

    private bool SaveInputBindingOverridesForController(PlayerController player, int controllerId)
    {
        PlayerControlOptionsData options = GetOrCreateControlOptions(controllerId);
        options.controllerId = controllerId;
        return SaveInputBindingOverrides(player, options);
    }

    private void ResetControlOptionsForController(PlayerController player, int controllerId)
    {
        PlayerControlOptionsData options = GetOrCreateControlOptions(controllerId);
        options.controllerId = controllerId;
        options.relativeInputs = false;
        options.toggleCodeInput = false;
        options.tapJump = false;
        options.vibeCoding = false;
        options.downJumpSlide = false;
        SaveInputBindingOverrides(player, options);
    }

    private bool MergeInputBindingOverridesForController(
        PlayerController player,
        int controllerId,
        string[] changedBindingIds)
    {
        if (changedBindingIds == null || changedBindingIds.Length == 0)
        {
            return false;
        }

        InputActionMap actionMap = GetPlayerActionMap(player);
        if (actionMap == null)
        {
            return false;
        }

        PlayerControlOptionsData options = GetOrCreateControlOptions(controllerId);
        options.controllerId = controllerId;
        InputActionMap[] temporaryMaps = null;
        try
        {
            // Rebuild an unmodified copy with the same action/binding GUIDs, then layer the
            // destination device's saved profile onto it. This lets conflict swapping use that
            // profile's real bindings instead of the pinned online profile's different map.
            temporaryMaps = InputActionMap.FromJson(actionMap.ToJson());
            if (temporaryMaps == null || temporaryMaps.Length == 0 || temporaryMaps[0] == null)
            {
                return false;
            }

            InputActionMap destinationMap = temporaryMaps[0];
            destinationMap.LoadBindingOverridesFromJson(options.inputBindingOverridesJson);

            bool appliedAnyBinding = false;
            for (int i = 0; i < changedBindingIds.Length; i++)
            {
                string bindingId = changedBindingIds[i];
                if (string.IsNullOrEmpty(bindingId)
                    || !TryFindBindingById(actionMap, bindingId, out InputAction liveAction, out int liveBindingIndex)
                    || !TryFindBindingById(destinationMap, bindingId, out InputAction destinationAction, out int destinationBindingIndex))
                {
                    continue;
                }

                InputBinding liveBinding = liveAction.bindings[liveBindingIndex];
                InputBinding destinationBinding = destinationAction.bindings[destinationBindingIndex];
                string previousDestinationPath = destinationBinding.effectivePath;
                string newBindingPath = liveBinding.effectivePath;
                if (string.IsNullOrEmpty(newBindingPath))
                {
                    continue;
                }

                ApplyPathOverridePreservingOtherOverrides(
                    destinationAction,
                    destinationBindingIndex,
                    newBindingPath);
                SwapConflictingBindingInMap(
                    destinationMap,
                    destinationAction,
                    newBindingPath,
                    previousDestinationPath);
                appliedAnyBinding = true;
            }

            if (!appliedAnyBinding)
            {
                return false;
            }

            options.inputBindingsSaved = true;
            options.inputBindingOverridesJson = destinationMap.SaveBindingOverridesAsJson();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to merge saved input bindings. Error: {exception.Message}");
            return false;
        }
        finally
        {
            if (temporaryMaps != null)
            {
                for (int i = 0; i < temporaryMaps.Length; i++)
                {
                    temporaryMaps[i]?.Dispose();
                }
            }
        }
    }

    private bool TryFindBindingById(
        InputActionMap actionMap,
        string bindingId,
        out InputAction action,
        out int bindingIndex)
    {
        action = null;
        bindingIndex = -1;
        if (actionMap == null || string.IsNullOrEmpty(bindingId))
        {
            return false;
        }

        foreach (InputAction candidateAction in actionMap.actions)
        {
            if (candidateAction == null)
            {
                continue;
            }

            for (int i = 0; i < candidateAction.bindings.Count; i++)
            {
                if (!string.Equals(
                    candidateAction.bindings[i].id.ToString(),
                    bindingId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                action = candidateAction;
                bindingIndex = i;
                return true;
            }
        }

        return false;
    }

    private void SwapConflictingBindingInMap(
        InputActionMap actionMap,
        InputAction reboundAction,
        string newBindingPath,
        string previousBindingPath)
    {
        if (actionMap == null
            || reboundAction == null
            || string.IsNullOrEmpty(newBindingPath)
            || string.IsNullOrEmpty(previousBindingPath)
            || BindingPathsMatch(newBindingPath, previousBindingPath))
        {
            return;
        }

        foreach (InputAction action in actionMap.actions)
        {
            if (action == null || action == reboundAction)
            {
                continue;
            }

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite
                    || binding.isPartOfComposite
                    || string.IsNullOrEmpty(binding.effectivePath)
                    || !BindingPathsMatch(binding.effectivePath, newBindingPath))
                {
                    continue;
                }

                ApplyPathOverridePreservingOtherOverrides(action, i, previousBindingPath);
                return;
            }
        }
    }

    private void ApplyPathOverridePreservingOtherOverrides(
        InputAction action,
        int bindingIndex,
        string overridePath)
    {
        InputBinding binding = action.bindings[bindingIndex];
        action.ApplyBindingOverride(bindingIndex, new InputBinding
        {
            overridePath = overridePath,
            overrideInteractions = binding.overrideInteractions,
            overrideProcessors = binding.overrideProcessors
        });
    }

    private bool BindingPathsMatch(string firstPath, string secondPath)
    {
        return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                CanonicalizeBindingPath(firstPath),
                CanonicalizeBindingPath(secondPath),
                StringComparison.OrdinalIgnoreCase);
    }

    private string CanonicalizeBindingPath(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath) || bindingPath[0] != '/')
        {
            return bindingPath;
        }

        int deviceEndIndex = bindingPath.IndexOf('/', 1);
        if (deviceEndIndex < 0)
        {
            return $"<{bindingPath.Substring(1)}>";
        }

        string deviceName = bindingPath.Substring(1, deviceEndIndex - 1);
        return $"<{deviceName}>{bindingPath.Substring(deviceEndIndex)}";
    }

    private bool SaveInputBindingOverrides(PlayerController player, PlayerControlOptionsData options)
    {
        if (options == null)
        {
            return false;
        }

        InputActionMap actionMap = GetPlayerActionMap(player);
        if (actionMap == null)
        {
            return false;
        }

        options.inputBindingsSaved = true;
        options.inputBindingOverridesJson = actionMap.SaveBindingOverridesAsJson();
        return true;
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

        // Resolve the local online player's profile before inspecting PlayerInput. On a joining
        // client, lower-numbered remote prefabs spawn first and can otherwise change device order.
        // IsOnlineMatchInitializing is required because the first apply happens before the active
        // flag is raised.
        if (IsOnlineLocalPlayer(player))
        {
            if (onlineLocalControllerId < 0
                && !TryChooseOnlineControllerId(player, out onlineLocalControllerId))
            {
                return false;
            }

            controllerId = onlineLocalControllerId;
            return true;
        }

        return TryGetDirectControllerId(player, out controllerId);
    }

    private bool TryGetDirectControllerId(PlayerController player, out int controllerId)
    {
        controllerId = -1;
        if (player == null)
        {
            return false;
        }

        InputDevice activeInputDevice = player.inputs != null
            ? player.inputs.ActiveInputDevice
            : null;
        if (activeInputDevice != null && InputDeviceManager.IsValidInput(activeInputDevice))
        {
            controllerId = activeInputDevice.deviceId;
            return true;
        }

        PlayerInput playerInput = player.GetComponent<PlayerInput>();
        if (playerInput != null
            && playerInput.devices.Count > 0
            && playerInput.devices[0] != null
            && InputDeviceManager.IsValidInput(playerInput.devices[0]))
        {
            controllerId = playerInput.devices[0].deviceId;
            return true;
        }

        return false;
    }

    private bool TryChooseOnlineControllerId(PlayerController player, out int controllerId)
    {
        controllerId = -1;

        if (ControlOptions == null)
        {
            LoadControlOptions();
        }

        // A cold/deferred join may have no live pre-online player to capture. If exactly one saved
        // profile belongs to a connected device, it is the least ambiguous carry-over.
        int savedConnectedControllerId = -1;
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device == null
                || !InputDeviceManager.IsValidInput(device)
                || FindControlOptions(device.deviceId) == null)
            {
                continue;
            }

            if (savedConnectedControllerId >= 0)
            {
                savedConnectedControllerId = -1;
                break;
            }

            savedConnectedControllerId = device.deviceId;
        }

        if (savedConnectedControllerId >= 0)
        {
            controllerId = savedConnectedControllerId;
            return true;
        }

        if (TryGetDirectControllerId(player, out controllerId))
        {
            return true;
        }

        // Last resort for a player that spawned without a valid InputUser. Cache this choice once
        // so later pairing or active-device changes cannot switch profiles during the match.
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device != null && InputDeviceManager.IsValidInput(device))
            {
                controllerId = device.deviceId;
                return true;
            }
        }

        return false;
    }

    private bool IsOnlineLocalPlayer(PlayerController player)
    {
        GameManager manager = GameManager.Instance;
        return player != null
            && manager != null
            && (manager.isOnlineMatchActive || manager.IsOnlineMatchInitializing)
            && manager.players != null
            && manager.localPlayerIndex >= 0
            && manager.localPlayerIndex < manager.players.Length
            && manager.players[manager.localPlayerIndex] == player;
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

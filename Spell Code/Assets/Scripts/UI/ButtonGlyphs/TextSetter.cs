using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;


public class TextSetter : MonoBehaviour
{
    [SerializeField] public string stringToReplace = "BUTTONPROMPT";
    [NonSerialized] public string referenceString;
    [SerializeField] private bool useModifiedString = false;
    [SerializeField] private SpriteAssetList spriteAssets;
    [SerializeField] private DeviceType deviceType;
    [SerializeField] public InputActionReference defaultAction;
    public bool nullInput = false;
    [SerializeField] public bool pressedOverride = false;
    public int selectorPID = 0;

    private TMP_Text _textBox;
    private enum DeviceType
    {
        Keyboard,
        Gamepad
    }

    private void Awake()
    {
        EnsureTextBoxInitialized();
    }

    private void Start()
    {
        //debug test 
        SetText(defaultAction);
    }

    public void UpdateGlyph()
    {
        SetText(defaultAction);
    }

    public void SetSelectorPID(int playerId)
    {
        selectorPID = playerId;
        UpdateGlyph();
    }

    public void ClearGlyph()
    {
        if (!EnsureTextBoxInitialized())
        {
            return;
        }

        _textBox.text = referenceString.Replace(stringToReplace, string.Empty);
    }

    public InputAction TargetAction
    {
        get { return defaultAction != null ? defaultAction.action : null; }
    }

    public void SetText(InputAction targetAction)
    {
        if (!EnsureTextBoxInitialized() || spriteAssets == null || spriteAssets.spriteAssets == null)
        {
            return;
        }

        
        string inputMessage = useModifiedString?_textBox.text:referenceString;
        InputBinding targetBinding;
        PlayerController selectedPlayer = GetSelectedPlayer();
        if(selectedPlayer == null || selectedPlayer.inputs == null)
        {
            //Debug.LogError("Player ID either not set or not found.");
            deviceType = GetDefaultDeviceType();
            targetBinding = GetBindingForAction(targetAction,deviceType);
        }
        else
        {
            // ActiveInputDevice, not InputDevice: online the local player is paired with every
            // shared device at once, so devices[0] is always the keyboard regardless of what they
            // are really holding.
            InputDevice activeDevice = selectedPlayer.inputs.ActiveInputDevice;
            deviceType = activeDevice != null ? GetDeviceType(activeDevice) : GetDefaultDeviceType();
            targetBinding = GetBindingForAction(GetPlayerAction(selectedPlayer, targetAction),deviceType);
        }


        int spriteAssetIndex = (int)deviceType;
        if (spriteAssetIndex < 0
            || spriteAssetIndex >= spriteAssets.spriteAssets.Count
            || spriteAssets.spriteAssets[spriteAssetIndex] == null)
        {
            Debug.LogError($"missing Sprite Asset for {deviceType}");
            return;
        }

        
        _textBox.text = ButtonPromptCompleter.ReadAndReplaceBinding(
            inputMessage,
            stringToReplace,
            targetBinding,
            spriteAssets.spriteAssets[spriteAssetIndex],
            pressedOverride,
            nullInput);
    }

    private bool EnsureTextBoxInitialized()
    {
        if (_textBox == null)
        {
            _textBox = GetComponent<TMP_Text>();
        }

        if (_textBox == null)
        {
            return false;
        }

        if (referenceString == null)
        {
            referenceString = _textBox.text ?? string.Empty;
        }

        return true;
    }

    private PlayerController GetSelectedPlayer()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null || manager.players == null || selectorPID <= 0 || selectorPID > manager.players.Length)
        {
            return null;
        }

        return manager.players[selectorPID - 1];
    }

    private DeviceType GetDeviceType(InputDevice inputDevice)
    {
        if (inputDevice is Keyboard)
        {
            return DeviceType.Keyboard;
        }

        if (inputDevice is Gamepad)
        {
            return DeviceType.Gamepad;
        }

        Debug.LogWarning($"Unsupported input device type: {inputDevice?.displayName ?? "None"}");
        return DeviceType.Keyboard;
    }

    private DeviceType GetDefaultDeviceType()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.added)
            {
                Debug.Log($"Detected gamepad: {gamepad.displayName} | {gamepad.layout}");
                return DeviceType.Gamepad;
            }
        }

        return DeviceType.Keyboard;
    }

    private InputBinding GetBindingForAction(InputAction inputAction, DeviceType inputDevice)
    {
        if (inputAction == null)
        {
            Debug.LogWarning("Binding not found because the target action is missing.");
            return default;
        }

        if (inputDevice == DeviceType.Keyboard)
        {
            return GetFirstBindingForDevicePath(inputAction, "<Keyboard>");
        }

        if (inputDevice == DeviceType.Gamepad)
        {
            return GetFirstBindingForDevicePath(inputAction, "<Gamepad>");
        }

        Debug.LogWarning($"Binding not found, returning first possible binding");
        return inputAction.bindings[0];
    }

    private InputAction GetPlayerAction(PlayerController player, InputAction defaultInputAction)
    {
        if (player == null || defaultInputAction == null)
        {
            return defaultInputAction;
        }

        InputActionMap playerActionMap = player.inputs != null ? player.inputs.PlayerActionMap : null;
        if (playerActionMap == null)
        {
            return defaultInputAction;
        }

        InputAction playerAction = playerActionMap.FindAction(defaultInputAction.name, false);
        return playerAction != null ? playerAction : defaultInputAction;
    }

    private InputBinding GetFirstBindingForDevicePath(InputAction inputAction, string devicePath)
    {
        foreach (InputBinding binding in inputAction.bindings)
        {
            string bindingPath = binding.effectivePath;
            if (!string.IsNullOrEmpty(bindingPath) && bindingPath.StartsWith(devicePath))
            {
                return binding;
            }
        }

        Debug.LogWarning($"No {devicePath} binding found for action {inputAction.name}.");
        return inputAction.bindings[0];
    }
}

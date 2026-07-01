using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.iOS;


public class TextSetter : MonoBehaviour
{
    [SerializeField] private string message = "Press BUTTONPROMPT to start";
    [SerializeField] private SpriteAssetList spriteAssets;
    [SerializeField] private DeviceType deviceType;
    [SerializeField] private InputActionReference defaultAction;
    private int selectorPID = 0;

    private TMP_Text _textBox;
    private enum DeviceType
    {
        Keyboard,
        Gamepad
    }

    private void Awake()
    {
        //_playerInput = new PlayerInput();
        _textBox = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        //debug test 
        SetText(message, defaultAction);
    }

    public void UpdateGlyphType()
    {
        SetText(message, defaultAction);
    }

    public void SetText(string inputMessage, InputAction targetAction)
    {   
        InputBinding targetBinding;
        if(selectorPID == 0|| GameManager.Instance.players[selectorPID-1]== null)
        {
            //Debug.LogError("Player ID either not set or not found.");
            deviceType = GetDefaultDeviceType();
            targetBinding = GetBindingForAction(targetAction,deviceType);
        }
        else
        {
            deviceType = GetDeviceType(GameManager.Instance.players[selectorPID-1].inputs.InputDevice);
            targetBinding = GetBindingForAction(targetAction,deviceType);
        }

        
        if((int)deviceType > spriteAssets.spriteAssets.Count - 1)
        {
            Debug.LogError($"missing Sprite Asset for {deviceType}");
            return;
        }

        
        _textBox.text = ButtonPromptCompleter.ReadAndReplaceBinding(inputMessage, targetBinding,spriteAssets.spriteAssets[(int)deviceType]);
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

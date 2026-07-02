using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.iOS;


public class TextSetter : MonoBehaviour
{
    [SerializeField] private string message = "Press BUTTONPROMPT to start";
    [SerializeField] private SpriteAssetList spriteAssets;
    [SerializeField] private DeviceType deviceType;
    private int selectorPID = 0;

    //private PlayerInput _playerInput;
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
        selectorPID = 1;
        if(GameManager.Instance.players[selectorPID-1]== null)
        {
            Debug.LogError("Player ID not set.");
            return;
        }
        PlayerController targetPlayer = GameManager.Instance.players[selectorPID-1];
        InputAction targetAction = targetPlayer.inputs.PauseAction;
        SetText(message, targetAction);
    }

    private void SetText(string inputMessage, InputAction targetAction)
    {   
        if(selectorPID == 0|| GameManager.Instance.players[selectorPID-1]== null)
        {
            Debug.LogError("Player ID either not set or not found.");
            return;
        }

        deviceType = GetDeviceType(GameManager.Instance.players[selectorPID-1].inputs.InputDevice);


        
        if((int)deviceType > spriteAssets.spriteAssets.Count - 1)
        {
            Debug.Log($"missing Sprite Asset for {deviceType}");
            return;
        }
        InputBinding targetBinding = GetBindingForAction(targetAction,GameManager.Instance.players[selectorPID-1].inputs.InputDevice);
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

    private InputBinding GetBindingForAction(InputAction inputAction, InputDevice inputDevice)
    {
        if (inputDevice is Keyboard)
        {
            return inputAction.bindings[0];
        }

        if (inputDevice is Gamepad)
        {
            return inputAction.bindings[1];
        }

        Debug.LogWarning($"Unsupported input device type: {inputDevice?.displayName ?? "None"}");
        return inputAction.bindings[0];
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class InputDeviceManager : MonoBehaviour
{
    public static InputDeviceManager Instance { get; private set; }
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    // ===== | Variables | =====
    private ReadOnlyArray<InputDevice> connectedInputDevices;

    [SerializeField] private InputPlayerBindings inputPlayerBindings;

    // ===== | Properties | =====
    public ReadOnlyArray<InputDevice> ConnectedInputDevices { get { return connectedInputDevices; } }

    // ===== | Methods | =====
    private void Start()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        GetConnectedInputDevices();
    }

    private void OnDeviceChange(InputDevice arg1, InputDeviceChange arg2)
    {
        GetConnectedInputDevices();
    }

    public void GetConnectedInputDevices()
    {
        connectedInputDevices = InputSystem.devices;

        foreach (InputDevice device in connectedInputDevices)
        {
            if (IsValidInput(device))
            {
                if (device is Gamepad)
                {
                    //if (inputPlayerBindings != null)
                    //    inputPlayerBindings.AssignInputDevice(device);
                }
            }
        }
    }

    private void OnGUI()
    {
        // Display connected input devices
        //GUILayout.Label("Connected Input Devices:");
        //foreach (InputDevice device in connectedInputDevices)
        //{
        //    if (IsValidInput(device))
        //        GUILayout.Label($"{device.name}: {device.layout}");
        //}
    }

    public static bool IsValidInput(InputDevice device)
    {
        switch (device)
        {
            case Gamepad _:
            case Keyboard _:
            case Joystick _:
                return true;
            default:
                return false;
        }
    }
}

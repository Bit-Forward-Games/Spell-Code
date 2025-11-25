using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Processors;
using UnityEngine.InputSystem.Utilities;

public class InputBuffer : ISerialize
{
    // ===== | Variables | =====
    private short[] inputQueue;

    // ===== | Constructor | =====
    public InputBuffer()
    {
        inputQueue = new short[30];

        // Set all elements to -1 to indicate that they are empty
        ClearBuffer();
    }

    // ===== | Properties | =====
    public short[] InputQueue { get { return inputQueue; } }

    // ===== | Methods | =====
    public void Push(short input)
    {
        for (int i = inputQueue.Length - 1; i >= 0; i--)
        {
            if (i == 0)
            {
                inputQueue[i] = input;
            }
            else
            {
                inputQueue[i] = inputQueue[i - 1];
            }
        }
    }

    public bool SequenceInBuffer(short[] sequence, int tolerance = -1)
    {
        if ((tolerance != -1) && (tolerance < sequence.Length))
        {
            Debug.LogWarning("Input Buffer was given a sequence it cannot do:\n" +
                $"InputBuffer length: {tolerance}, " +
                $"Sequence Length {sequence.Length}");
        }

        int sequenceIndex = sequence.Length;

        int checkLength = tolerance != -1 ? tolerance : inputQueue.Length;

        for (int i = 0; i < checkLength; i++)
        {
            if (sequenceIndex == 0)
            {
                return true;
            }

            short inputDirection =
                 BitConverter.GetBytes(inputQueue[i])[0];

            if (inputDirection == sequence[sequenceIndex - 1])
            {
                sequenceIndex--;
            }
        }

        return false;
    }

    public void ClearBuffer()
    {
        for (int i = 0; i < inputQueue.Length; i++)
        {
            inputQueue[i] = -1;
        }
    }
    
    public void Deserialize(BinaryReader read)
    {
        for (int i = 0; i < inputQueue.Length; i++)
        {
            inputQueue[i] = read.ReadInt16();
        }
    }

    public void Serialize(BinaryWriter write)
    {
        foreach (short input in inputQueue)
        {
            write.Write(input);
        }
    }
}

public class InputPlayerBindings : MonoBehaviour
{
    // ===== | Variables | =====
    [SerializeField] private InputActionAsset inputActionAsset;
    [SerializeField] private InputActionMap playerActionMap;

    private InputAction upAction;
    private InputAction downAction;
    private InputAction leftAction;
    private InputAction rightAction;
    private InputAction codeAction;
    private InputAction jumpAction;

    private bool[] direction = new bool[4];
    private bool[] codeButton = new bool[2];
    private bool[] jumpButton = new bool[2];
    private ButtonState[] buttons = new ButtonState[2];

    InputBuffer inputBuffer = new InputBuffer();

    // ===== | Properties | =====
    public InputAction UpAction { get { return upAction; } }
    public InputAction DownAction { get { return downAction; } }
    public InputAction LeftAction { get { return leftAction; } }
    public InputAction RightAction { get { return rightAction; } }
    public InputAction CodeAction { get { return codeAction; } }
    public InputAction JumpAction { get { return jumpAction; } }
    public InputDevice InputDevice { get { return inputActionAsset.devices.Value[0]; } }
    public InputActionMap PlayerActionMap { get { return playerActionMap; } }
    public InputSnapshot CurrentSnapshop { get; private set; }

    public InputBuffer InputBuffer
    {
        get { return inputBuffer; }
    }

    public bool IsActive { get; private set; } = true;

    // ===== | Methods | =====

    // Constructor
    public void Awake()
    {
        //inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
        playerActionMap = inputActionAsset.actionMaps[0];

        upAction = playerActionMap.FindAction("Up");
        downAction = playerActionMap.FindAction("Down");
        leftAction = playerActionMap.FindAction("Left");
        rightAction = playerActionMap.FindAction("Right");
        codeAction = playerActionMap.FindAction("Code");
        jumpAction = playerActionMap.FindAction("Jump");

        playerActionMap.Enable();
        inputActionAsset.Enable();
    }

    //private void OnDisable()
    //{
    //    inputActionAsset.Disable();
    //    playerActionMap.Disable();
    //    playerActionMap.Dispose();
    //    upAction.Dispose();
    //    downAction.Dispose();
    //    leftAction.Dispose();
    //    rightAction.Dispose();
    //    codeAction.Dispose();
    //    jumpAction.Dispose();
    //    upAction = null;
    //    downAction = null;
    //    leftAction = null;
    //    rightAction = null;
    //    codeAction = null;
    //    jumpAction = null;
    //    playerActionMap = null;
    //    inputActionAsset = null;
    //}

    private void OnGUI()
    {
        if (IsActive)
        {
            //GUILayout.Label("Current Input Snapshot:");
            //string buffer = "Input Buffer: ";

            //foreach (short input in inputBuffer.InputQueue)
            //{
            //    buffer += $"{input}, ";
            //}

            //GUILayout.Label(buffer);
        }
    }

    private void SetupInputAsset()
    {
        //inputActionAsset = new InputActionAsset();
        playerActionMap = inputActionAsset.AddActionMap("Player");

        upAction = playerActionMap.AddAction("Up", InputActionType.Button);
        downAction = playerActionMap.AddAction("Down", InputActionType.Button);
        leftAction = playerActionMap.AddAction("Left", InputActionType.Button);
        rightAction = playerActionMap.AddAction("Right", InputActionType.Button);
        codeAction = playerActionMap.AddAction("Code", InputActionType.Button);
        jumpAction = playerActionMap.AddAction("Jump", InputActionType.Button);

        playerActionMap.Enable();
        inputActionAsset.Enable();
    }

    public void AssignInputDevice(InputDevice inputDevice)
    {
        if (inputActionAsset == null || playerActionMap == null)
        {
            SetupInputAsset();
        }
        #region nah
        //if (inputDevice != null)
        //{
        //    inputActionAsset.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { inputDevice });

        //    string[][] bindings = DefaultInputBindings.SetControllerBindings(inputDevice);

        //    for (int i = 0; i < bindings.Length; i++)
        //    {
        //        for (int j = 0; j < bindings[i].Length; j++)
        //        {
        //            switch (i)
        //            {
        //                case 0:
        //                    playerActionMap.FindAction("Up").AddBinding(bindings[i][j]).WithProcessor("axisDeadzone(min=0.9)");
        //                    break;
        //                case 1:
        //                    playerActionMap.FindAction("Down").AddBinding(bindings[i][j]).WithProcessor("axisDeadzone(min=0.9)");
        //                    break;
        //                case 2:
        //                    playerActionMap.FindAction("Left").AddBinding(bindings[i][j]).WithProcessor("axisDeadzone(min=0.9)");
        //                    break;
        //                case 3:
        //                    playerActionMap.FindAction("Right").AddBinding(bindings[i][j]).WithProcessor("axisDeadzone(min=0.9)");
        //                    break;
        //                case 4:
        //                    playerActionMap.FindAction("Code").AddBinding(bindings[i][j]);
        //                    break;
        //                case 5:
        //                    playerActionMap.FindAction("Jump").AddBinding(bindings[i][j]);
        //                    break;
        //            }
        //        }
        //    }

        //    IsActive = true;
        //}
        //else
        //{
        //    //Debug.Log("True");
        //    AssignInputDevice();
        //}
        #endregion

        AssignInputDevice();
    }
    //public void AssignInputDevice(InputActionMap map, InputDevice device)
    //{
    //    playerActionMap.Disable();
    //    inputActionAsset.Disable();

    //    inputActionAsset.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { device });
    //    inputActionAsset.AddActionMap(SCUtils.CreateCloneMap(map));
    //    playerActionMap = map;

    //    upAction = playerActionMap.FindAction("Up");
    //    downAction = playerActionMap.FindAction("Down");
    //    leftAction = playerActionMap.FindAction("Left");
    //    rightAction = playerActionMap.FindAction("Right");
    //    codeAction = playerActionMap.FindAction("Code");
    //    jumpAction = playerActionMap.FindAction("Jump");
    //    IsActive = true;

    //    playerActionMap.Enable();
    //    inputActionAsset.Enable();
    //}

    /// <summary>
    /// This overload is used for development, if opening up the game scene directly 
    /// it will use the connected input action asset. Can be removed on release
    /// </summary>
    private void AssignInputDevice()
    {
        if (GetComponent<PlayerInput>() is PlayerInput action)
        {
            inputActionAsset = action.actions;
            //inputActionAsset.devices = InputSystem.devices;

            //playerActionMap = inputActionAsset.FindActionMap("Gameplay");
            playerActionMap = action.currentActionMap;

            upAction = playerActionMap.FindAction("Up");
            downAction = playerActionMap.FindAction("Down");
            leftAction = playerActionMap.FindAction("Left");
            rightAction = playerActionMap.FindAction("Right");
            codeAction = playerActionMap.FindAction("Code");
            jumpAction = playerActionMap.FindAction("Jump");
            IsActive = true;
        }
    }


    public long UpdateInputs()
    {
        direction[0] = upAction.ReadValue<float>() > 0.33f;
        direction[1] = downAction.ReadValue<float>() > 0.33f;
        direction[2] = leftAction.ReadValue<float>() > 0.33f;
        direction[3] = rightAction.ReadValue<float>() > 0.33f;
        //direction[0] = upAction.inProgress;
        //direction[1] = downAction.inProgress;
        //direction[2] = leftAction.inProgress;
        //direction[3] = rightAction.inProgress;

        codeButton[0] = codeButton[1];
        jumpButton[0] = jumpButton[1];

        codeButton[1] = codeAction.inProgress;
        jumpButton[1] = jumpAction.inProgress;

        buttons[0] = GetCurrentState(codeButton[0], codeButton[1]);
        buttons[1] = GetCurrentState(jumpButton[0], jumpButton[1]);

        inputBuffer.Push(InputConverter.ConvertToShort(buttons, direction));

        CurrentSnapshop = InputConverter.ConvertFromShort(InputConverter.ConvertToShort(buttons, direction));

        return InputConverter.ConvertToLong(buttons, direction);
    }

    private ButtonState GetCurrentState(bool previous, bool current)
    {
        if (!previous && !current)
        {
            return ButtonState.None;
        }
        else if (current && !previous)
        {
            return ButtonState.Pressed;
        }
        else if (current && previous)
        {
            return ButtonState.Held;
        }
        else
        {
            return ButtonState.Released;
        }
    }

    public void CheckForInputs(bool enable)
    {
        if (enable)
        {
            upAction.Enable();
            downAction.Enable();
            leftAction.Enable();
            rightAction.Enable();
            codeAction.Enable();
            jumpAction.Enable();
        }
        else
        {
            upAction.Disable();
            downAction.Disable();
            leftAction.Disable();
            rightAction.Disable();
            codeAction.Disable();
            jumpAction.Disable();
        }
    }
}

using System;
using System.Collections.Generic;
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

/// <summary>
/// Raw, device-level gameplay state captured between deterministic simulation ticks.
/// This state is local-only and must never be serialized as rollback state.
/// </summary>
public struct OnlineRawInputState : IEquatable<OnlineRawInputState>
{
    public bool Up;
    public bool Down;
    public bool Left;
    public bool Right;
    public bool Code;
    public bool Jump;
    public bool Slide;

    public bool Equals(OnlineRawInputState other)
    {
        return Up == other.Up
            && Down == other.Down
            && Left == other.Left
            && Right == other.Right
            && Code == other.Code
            && Jump == other.Jump
            && Slide == other.Slide;
    }

    public override bool Equals(object obj)
    {
        return obj is OnlineRawInputState other && Equals(other);
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (Up ? 1 : 0);
        hash = hash * 31 + (Down ? 1 : 0);
        hash = hash * 31 + (Left ? 1 : 0);
        hash = hash * 31 + (Right ? 1 : 0);
        hash = hash * 31 + (Code ? 1 : 0);
        hash = hash * 31 + (Jump ? 1 : 0);
        hash = hash * 31 + (Slide ? 1 : 0);
        return hash;
    }
}

/// <summary>
/// Buffers physical input transitions until a new online input frame accepts them. Peek is
/// deliberately non-destructive so held network ticks cannot consume a Pressed/Released edge.
/// </summary>
public sealed class OnlineInputCaptureBuffer
{
    private const int MaxPendingStates = 32;

    private readonly Queue<OnlineRawInputState> pendingStates = new Queue<OnlineRawInputState>();
    private OnlineRawInputState lastCapturedState;
    private OnlineRawInputState committedState;
    private OnlineRawInputState peekedState;
    private bool initialized;
    private bool hasPeekedState;
    private bool peekedFromQueue;

    public int PendingCount => pendingStates.Count;
    public OnlineRawInputState CommittedState => committedState;

    public void Reset(OnlineRawInputState baseline)
    {
        pendingStates.Clear();
        lastCapturedState = baseline;
        committedState = baseline;
        peekedState = baseline;
        initialized = true;
        hasPeekedState = false;
        peekedFromQueue = false;
    }

    public void Clear()
    {
        pendingStates.Clear();
        lastCapturedState = default;
        committedState = default;
        peekedState = default;
        initialized = false;
        hasPeekedState = false;
        peekedFromQueue = false;
    }

    public void Capture(OnlineRawInputState state)
    {
        if (!initialized)
        {
            Reset(state);
            return;
        }

        if (state.Equals(lastCapturedState))
        {
            return;
        }

        lastCapturedState = state;
        if (pendingStates.Count >= MaxPendingStates)
        {
            if (!hasPeekedState || !peekedFromQueue)
            {
                pendingStates.Dequeue();
            }
            else
            {
                // Preserve the candidate awaiting scheduler acknowledgement. The newest physical
                // state remains in lastCapturedState and is emitted after the bounded queue drains.
                return;
            }
        }

        pendingStates.Enqueue(state);
    }

    public OnlineRawInputState Peek()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("Online input capture must be reset before it is read.");
        }

        if (!hasPeekedState)
        {
            peekedFromQueue = pendingStates.Count > 0;
            peekedState = peekedFromQueue ? pendingStates.Peek() : lastCapturedState;
            hasPeekedState = true;
        }

        return peekedState;
    }

    public bool CommitPeeked()
    {
        if (!hasPeekedState)
        {
            return false;
        }

        if (peekedFromQueue)
        {
            if (pendingStates.Count == 0 || !pendingStates.Peek().Equals(peekedState))
            {
                throw new InvalidOperationException("The pending online input candidate changed before commit.");
            }

            pendingStates.Dequeue();
        }

        committedState = peekedState;
        hasPeekedState = false;
        peekedFromQueue = false;
        return true;
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
    private InputAction pauseAction;

    private InputAction slideMacroAction;
    private InputAction addBotAction;
    private InputAction removeBotAction;

    // Last device that actually drove one of this player's actions. See ActiveInputDevice.
    private InputDevice lastUsedDevice;

    private bool[] direction = new bool[4];
    private bool[] codeButton = new bool[2];
    private bool[] jumpButton = new bool[2];

    private bool[] pauseButton = new bool[2];
    private ButtonState[] buttons = new ButtonState[3];

    // Lobby commands, not gameplay. These deliberately stay out of the packed InputSnapshot -- they
    // are addressed to the GameManager, never to a character, so nothing downstream of the sim needs
    // to know about them. Their edges are still derived here, from a level read once per fixed tick,
    // for the same reason every other button's are: sampling a press edge from a MonoBehaviour
    // Update is a render-vs-fixed race that survives the editor and dies in a vsynced build.
    private bool[] addBotButton = new bool[2];
    private bool[] removeBotButton = new bool[2];

    public ButtonState AddBotState { get; private set; }
    public ButtonState RemoveBotState { get; private set; }

    // Online rollback can inspect the same simulation frame more than once while pacing holds it.
    // Keep physical transitions separate from the legacy FixedUpdate sampler so a rejected duplicate
    // target cannot consume a one-frame Pressed/Released edge. Offline input remains unchanged.
    private readonly OnlineInputCaptureBuffer onlineInputCapture = new OnlineInputCaptureBuffer();
    private InputAction[] subscribedOnlineActions = Array.Empty<InputAction>();
    private readonly bool[] onlineDirections = new bool[4];
    private readonly ButtonState[] onlineButtons = new ButtonState[3];
    private bool onlineCaptureEnabled;
    private bool onlineCaptureSuppressed;
    private bool ignoreOnlineActionCallbacks;
    private bool hasPeekedOnlineInput;
    private long peekedOnlineInput;

    InputBuffer inputBuffer = new InputBuffer();

    // ===== | Properties | =====
    public InputAction UpAction { get { return upAction; } }
    public InputAction DownAction { get { return downAction; } }
    public InputAction LeftAction { get { return leftAction; } }
    public InputAction RightAction { get { return rightAction; } }
    public InputAction CodeAction { get { return codeAction; } }
    public InputAction JumpAction { get { return jumpAction; } }
    public InputAction PauseAction { get { return pauseAction; } }
    public InputAction SlideMacroAction {get{return slideMacroAction;}}
    public InputDevice InputDevice { get { return inputActionAsset.devices.Value[0]; } }

    // The device this player is ACTUALLY using, for button-glyph rendering.
    public InputDevice ActiveInputDevice
    {
        get
        {
            if (lastUsedDevice != null && lastUsedDevice.added)
            {
                return lastUsedDevice;
            }

            if (inputActionAsset == null || !inputActionAsset.devices.HasValue)
            {
                return null;
            }

            ReadOnlyArray<InputDevice> devices = inputActionAsset.devices.Value;
            return devices.Count > 0 ? devices[0] : null;
        }
    }
    public InputActionMap PlayerActionMap { get { return playerActionMap; } }
    public InputSnapshot CurrentSnapshot { get; private set; }

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
        pauseAction = playerActionMap.FindAction("Pause");
        slideMacroAction = playerActionMap.FindAction("Slide");
        addBotAction = playerActionMap.FindAction("AddBot");
        removeBotAction = playerActionMap.FindAction("RemoveBot");

        playerActionMap.Enable();
        inputActionAsset.Enable();
        RefreshOnlineInputSubscriptions();
    }

    private void OnDestroy()
    {
        UnsubscribeOnlineInputActions();
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
        pauseAction = playerActionMap.AddAction("Pause", InputActionType.Button);
        slideMacroAction = playerActionMap.AddAction("Slide", InputActionType.Button);

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
            pauseAction = playerActionMap.FindAction("Pause");
            // PlayerInput gives each instantiated online player its own action-map clone.
            // Refresh Slide with the rest or joining clients keep reading the prefab map.
            slideMacroAction = playerActionMap.FindAction("Slide");
            addBotAction = playerActionMap.FindAction("AddBot");
            removeBotAction = playerActionMap.FindAction("RemoveBot");
            IsActive = true;
            RefreshOnlineInputSubscriptions();
        }
    }

    private void RefreshOnlineInputSubscriptions()
    {
        InputAction[] desiredActions = new[]
        {
            upAction,
            downAction,
            leftAction,
            rightAction,
            codeAction,
            jumpAction,
            slideMacroAction
        }
        .Where(action => action != null)
        .Distinct()
        .ToArray();

        bool actionsChanged = desiredActions.Length != subscribedOnlineActions.Length;
        if (!actionsChanged)
        {
            for (int i = 0; i < desiredActions.Length; i++)
            {
                if (!ReferenceEquals(desiredActions[i], subscribedOnlineActions[i]))
                {
                    actionsChanged = true;
                    break;
                }
            }
        }

        if (!actionsChanged)
        {
            return;
        }

        UnsubscribeOnlineInputActions();
        subscribedOnlineActions = desiredActions;
        for (int i = 0; i < subscribedOnlineActions.Length; i++)
        {
            subscribedOnlineActions[i].started += HandleOnlineActionChanged;
            subscribedOnlineActions[i].performed += HandleOnlineActionChanged;
            subscribedOnlineActions[i].canceled += HandleOnlineActionChanged;
        }

        if (onlineCaptureEnabled)
        {
            ResetOnlineInputCapture();
        }
    }

    private void UnsubscribeOnlineInputActions()
    {
        for (int i = 0; i < subscribedOnlineActions.Length; i++)
        {
            InputAction action = subscribedOnlineActions[i];
            if (action == null)
            {
                continue;
            }

            action.started -= HandleOnlineActionChanged;
            action.performed -= HandleOnlineActionChanged;
            action.canceled -= HandleOnlineActionChanged;
        }

        subscribedOnlineActions = Array.Empty<InputAction>();
    }

    private void HandleOnlineActionChanged(InputAction.CallbackContext context)
    {
        if (!onlineCaptureEnabled
            || onlineCaptureSuppressed
            || ignoreOnlineActionCallbacks
            || !IsActive)
        {
            return;
        }

        if (context.control?.device != null)
        {
            lastUsedDevice = context.control.device;
        }

        CaptureCurrentOnlineInputState();
    }

    private OnlineRawInputState ReadCurrentOnlineInputState()
    {
        return new OnlineRawInputState
        {
            Up = upAction != null && upAction.ReadValue<float>() > 0.33f,
            Down = downAction != null && downAction.ReadValue<float>() > 0.33f,
            Left = leftAction != null && leftAction.ReadValue<float>() > 0.33f,
            Right = rightAction != null && rightAction.ReadValue<float>() > 0.33f,
            Code = codeAction != null && codeAction.inProgress,
            Jump = jumpAction != null && jumpAction.inProgress,
            Slide = slideMacroAction != null && slideMacroAction.inProgress
        };
    }

    private void CaptureCurrentOnlineInputState()
    {
        if (!onlineCaptureEnabled || onlineCaptureSuppressed || ignoreOnlineActionCallbacks)
        {
            return;
        }

        onlineInputCapture.Capture(ReadCurrentOnlineInputState());
    }

    public void EnableOnlineInputCapture()
    {
        RefreshOnlineInputSubscriptions();
        if (onlineCaptureEnabled)
        {
            return;
        }

        onlineCaptureEnabled = true;
        onlineCaptureSuppressed = false;
        ResetOnlineInputCapture();
    }

    public void DisableOnlineInputCapture()
    {
        onlineCaptureEnabled = false;
        onlineCaptureSuppressed = false;
        hasPeekedOnlineInput = false;
        onlineInputCapture.Clear();
    }

    public void SetOnlineInputCaptureSuppressed(bool suppressed)
    {
        if (!onlineCaptureEnabled)
        {
            if (!suppressed)
            {
                EnableOnlineInputCapture();
            }
            return;
        }

        if (onlineCaptureSuppressed == suppressed)
        {
            return;
        }

        onlineCaptureSuppressed = suppressed;
        // Inputs used to navigate pause/rebind UI must never burst into gameplay on resume.
        ResetOnlineInputCapture();
    }

    public void ResetOnlineInputCapture()
    {
        hasPeekedOnlineInput = false;
        onlineInputCapture.Reset(ReadCurrentOnlineInputState());
    }

    /// <summary>
    /// Returns the next online input candidate without consuming it. The candidate remains stable
    /// across pacing-held ticks until CommitPeekedOnlineInputs confirms a fresh target accepted it.
    /// </summary>
    public long PeekOnlineInputs()
    {
        EnableOnlineInputCapture();
        CaptureCurrentOnlineInputState();

        OnlineRawInputState previous = onlineInputCapture.CommittedState;
        OnlineRawInputState candidate = onlineInputCapture.Peek();

        onlineDirections[0] = candidate.Up;
        onlineDirections[1] = candidate.Down || candidate.Slide;
        onlineDirections[2] = candidate.Left;
        onlineDirections[3] = candidate.Right;

        onlineButtons[0] = GetCurrentState(previous.Code, candidate.Code);
        bool previousJumpOrSlide = previous.Jump || previous.Slide;
        bool currentJumpOrSlide = candidate.Jump || candidate.Slide;
        onlineButtons[1] = GetCurrentState(previousJumpOrSlide, currentJumpOrSlide);
        // Online pause is handled locally, outside deterministic simulation.
        onlineButtons[2] = ButtonState.None;

        peekedOnlineInput = InputConverter.ConvertToLong(onlineButtons, onlineDirections);
        hasPeekedOnlineInput = true;
        return peekedOnlineInput;
    }

    public bool CommitPeekedOnlineInputs()
    {
        if (!hasPeekedOnlineInput || !onlineInputCapture.CommitPeeked())
        {
            return false;
        }

        short packedInput = unchecked((short)peekedOnlineInput);
        inputBuffer.Push(packedInput);
        CurrentSnapshot = InputConverter.ConvertFromShort(packedInput);
        hasPeekedOnlineInput = false;
        return true;
    }


    // activeControl is non-null only while an action is actuated, so this latches the device that
    // last produced real input and ignores idle devices (a connected-but-unused pad never wins).
    private void TrackLastUsedDevice()
    {
        InputDevice device =
            upAction?.activeControl?.device ??
            downAction?.activeControl?.device ??
            leftAction?.activeControl?.device ??
            rightAction?.activeControl?.device ??
            codeAction?.activeControl?.device ??
            jumpAction?.activeControl?.device ??
            pauseAction?.activeControl?.device ??
            slideMacroAction?.activeControl?.device;

        if (device != null)
        {
            lastUsedDevice = device;
        }
    }

    public long UpdateInputs()
    {
        TrackLastUsedDevice();

        direction[0] = upAction.ReadValue<float>() > 0.33f;
        direction[1] = downAction.ReadValue<float>() > 0.33f;
        direction[2] = leftAction.ReadValue<float>() > 0.33f;
        direction[3] = rightAction.ReadValue<float>() > 0.33f;

        codeButton[0] = codeButton[1];
        jumpButton[0] = jumpButton[1];
        pauseButton[0] = pauseButton[1];
        

        codeButton[1] = codeAction.inProgress;
        jumpButton[1] = jumpAction.inProgress;
        pauseButton[1] = pauseAction.inProgress;

        if (slideMacroAction != null && slideMacroAction.inProgress)
        {
            direction[1] = true;
            jumpButton[1] = true;   
        }

        buttons[0] = GetCurrentState(codeButton[0], codeButton[1]);
        buttons[1] = GetCurrentState(jumpButton[0], jumpButton[1]);
        buttons[2] = GetCurrentState(pauseButton[0], pauseButton[1]);

        // Same previous/current edge derivation as the gameplay buttons above, but published on the
        // side rather than packed: GameManager reads these after the input gather to run the lobby's
        // add/remove-bot commands. Null-tolerant so an action map predating these bindings still runs.
        addBotButton[0] = addBotButton[1];
        removeBotButton[0] = removeBotButton[1];
        addBotButton[1] = addBotAction != null && addBotAction.inProgress;
        removeBotButton[1] = removeBotAction != null && removeBotAction.inProgress;
        AddBotState = GetCurrentState(addBotButton[0], addBotButton[1]);
        RemoveBotState = GetCurrentState(removeBotButton[0], removeBotButton[1]);

        

        inputBuffer.Push(InputConverter.ConvertToShort(buttons, direction));

        CurrentSnapshot = InputConverter.ConvertFromShort(InputConverter.ConvertToShort(buttons, direction));

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
        CheckForInputs(enable, true);
    }

    public void CheckForInputs(bool enable, bool assignKeyboardOnly)
    {
        IsActive = enable;
        if (enable)
        {
            if (assignKeyboardOnly)
            {
                // Offline split-screen relies on keyboard staying scoped to a single local player.
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && inputActionAsset != null)
                {
                    inputActionAsset.devices = new ReadOnlyArray<InputDevice>(new InputDevice[] { keyboard });
                    Debug.Log($"[CheckForInputs] Assigned keyboard '{keyboard.name}' to inputActionAsset");

                    Debug.Log($"[CheckForInputs] Up action bindings: {upAction?.bindings.Count ?? 0}");
                    if (upAction != null && upAction.bindings.Count > 0)
                    {
                        foreach (var binding in upAction.bindings)
                        {
                            Debug.Log($"  Binding: {binding.effectivePath}");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[CheckForInputs] Keyboard or inputActionAsset is null!");
                }
            }

            // Enable all actions
            playerActionMap?.Enable();
            upAction?.Enable();
            downAction?.Enable();
            leftAction?.Enable();
            rightAction?.Enable();
            codeAction?.Enable();
            jumpAction?.Enable();
            pauseAction?.Enable();
            slideMacroAction?.Enable();

            if (assignKeyboardOnly)
            {
                DisableOnlineInputCapture();
            }
            else
            {
                EnableOnlineInputCapture();
            }

            Debug.Log($"[CheckForInputs] Enabled - Actions enabled: " +
                     $"Up={upAction?.enabled}, Down={downAction?.enabled}, " +
                     $"Left={leftAction?.enabled}, Right={rightAction?.enabled}, " +
                     $"Slide={slideMacroAction?.enabled}");
        }
        else
        {
            ignoreOnlineActionCallbacks = true;
            DisableOnlineInputCapture();
            upAction?.Disable();
            downAction?.Disable();
            leftAction?.Disable();
            rightAction?.Disable();
            codeAction?.Disable();
            jumpAction?.Disable();
            pauseAction?.Disable();
            slideMacroAction?.Disable();
            ignoreOnlineActionCallbacks = false;

            Debug.Log($"[CheckForInputs] Disabled");
        }
    }

    public void SetActiveWithoutChangingActions(bool enable)
    {
        IsActive = enable;
        if (!enable)
        {
            DisableOnlineInputCapture();
        }
    }

    public void ConfigureInputDevices(params InputDevice[] devices)
    {
        if (inputActionAsset == null)
        {
            return;
        }

        InputDevice[] validDevices = devices?
            .Where(device => device != null && InputDeviceManager.IsValidInput(device))
            .Distinct()
            .ToArray();

        if (validDevices == null || validDevices.Length == 0)
        {
            return;
        }

        inputActionAsset.devices = new ReadOnlyArray<InputDevice>(validDevices);
    }

    public void AllowAllBindingGroups()
    {
        if (inputActionAsset != null)
        {
            inputActionAsset.bindingMask = null;
        }

        if (playerActionMap != null)
        {
            playerActionMap.bindingMask = null;
        }
    }
}

using System;
using System.IO;
using UnityEngine;

public class StressTestController : MonoBehaviour
{
    public static StressTestController Instance { get; private set; }

    [Header("Master")]
    public bool enableStressTest = false;

    [Header("Network Chaos")]
    public bool enableNetworkChaos = false;
    [Range(0f, 1f)] public float outboundLossChance = 0f;
    [Range(0f, 1f)] public float inboundLossChance = 0f;
    public int minLatencyMs = 0;
    public int maxLatencyMs = 0;
    public int jitterMs = 0;
    [Range(0f, 1f)] public float reorderChance = 0f;
    public int chaosSeed = 12345;

    [Header("Deterministic Input")]
    public bool enableDeterministicInput = false;
    public int inputSeed = 12345;
    public int directionChangeInterval = 10;
    public int buttonPulseInterval = 15;
    public int buttonHoldFrames = 4;

    [Header("State Hashing")]
    public bool enableStateHashing = false;
    public int hashSendIntervalFrames = 10;
    public bool dumpStateOnMismatch = true;

    private System.Random chaosRng;
    private System.Random inputRng;
    private int currentDirection = 5;
    private ButtonState codeState = ButtonState.None;
    private ButtonState jumpState = ButtonState.None;

    public bool IsActiveOnline =>
        enableStressTest &&
        GameManager.Instance != null &&
        GameManager.Instance.isOnlineMatchActive;

    public bool UseDeterministicInput =>
        IsActiveOnline &&
        enableDeterministicInput;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetForNewMatch()
    {
        chaosRng = new System.Random(chaosSeed);
        inputRng = new System.Random(inputSeed);
        currentDirection = 5;
        codeState = ButtonState.None;
        jumpState = ButtonState.None;
    }

    public int GetNetworkDelayMs()
    {
        if (chaosRng == null) chaosRng = new System.Random(chaosSeed);

        int baseLatency = 0;
        if (maxLatencyMs > 0 && maxLatencyMs >= minLatencyMs)
        {
            baseLatency = chaosRng.Next(minLatencyMs, maxLatencyMs + 1);
        }

        int jitter = 0;
        if (jitterMs > 0)
        {
            jitter = chaosRng.Next(-jitterMs, jitterMs + 1);
        }

        int total = baseLatency + jitter;
        return Math.Max(0, total);
    }

    public bool ShouldDropOutbound()
    {
        if (!enableNetworkChaos) return false;
        if (chaosRng == null) chaosRng = new System.Random(chaosSeed);
        return chaosRng.NextDouble() < outboundLossChance;
    }

    public bool ShouldDropInbound()
    {
        if (!enableNetworkChaos) return false;
        if (chaosRng == null) chaosRng = new System.Random(chaosSeed);
        return chaosRng.NextDouble() < inboundLossChance;
    }

    public bool ShouldReorder()
    {
        if (!enableNetworkChaos) return false;
        if (chaosRng == null) chaosRng = new System.Random(chaosSeed);
        return chaosRng.NextDouble() < reorderChance;
    }

    public ulong GetDeterministicInput(int frame)
    {
        if (inputRng == null) inputRng = new System.Random(inputSeed);

        if (directionChangeInterval > 0 && frame % directionChangeInterval == 0)
        {
            currentDirection = PickDirection();
        }

        UpdateButtonState(ref codeState, frame, buttonPulseInterval, buttonHoldFrames);
        UpdateButtonState(ref jumpState, frame + 7, buttonPulseInterval, buttonHoldFrames);

        ButtonState[] buttons = new ButtonState[2] { codeState, jumpState };
        bool[] directions = DirectionToBools(currentDirection);
        return (ulong)InputConverter.ConvertToLong(buttons, directions);
    }

    private int PickDirection()
    {
        int[] dirs = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        return dirs[inputRng.Next(dirs.Length)];
    }

    private void UpdateButtonState(ref ButtonState state, int frame, int interval, int holdFrames)
    {
        if (interval <= 0)
        {
            state = ButtonState.None;
            return;
        }

        int phase = frame % interval;
        if (phase == 0)
        {
            state = ButtonState.Pressed;
        }
        else if (phase < holdFrames)
        {
            state = ButtonState.Held;
        }
        else if (phase == holdFrames)
        {
            state = ButtonState.Released;
        }
        else
        {
            state = ButtonState.None;
        }
    }

    private bool[] DirectionToBools(int dir)
    {
        bool up = false;
        bool down = false;
        bool left = false;
        bool right = false;

        switch (dir)
        {
            case 7: up = true; left = true; break;
            case 8: up = true; break;
            case 9: up = true; right = true; break;
            case 4: left = true; break;
            case 6: right = true; break;
            case 1: down = true; left = true; break;
            case 2: down = true; break;
            case 3: down = true; right = true; break;
            default: break;
        }

        return new bool[] { up, down, left, right };
    }

    public string GetDumpDirectory()
    {
        string dir = Path.Combine(Application.persistentDataPath, "DesyncDumps");
        Directory.CreateDirectory(dir);
        return dir;
    }
}

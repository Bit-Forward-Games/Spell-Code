using System.Globalization;
using UnityEngine;

public class StressTestDebugUI : MonoBehaviour
{
    [Header("UI")]
    public bool showUI = false;
    public KeyCode toggleKey = KeyCode.F6;
    public Rect windowRect = new Rect(20, 20, 360, 480);

    private int presetIndex = 0;
    private static readonly string[] PresetNames = new string[]
    {
        "None",
        "Light (1% loss, 60-90ms)",
        "Medium (3% loss, 80-140ms)",
        "Heavy (5% loss, 120-200ms)",
        "Jitter (0-60ms, 2% loss)",
        "Reorder (2% reorder, 1% loss)"
    };

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showUI = !showUI;
        }
    }

    private void OnGUI()
    {
        if (!showUI) return;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Stress Test");
    }

    private void DrawWindow(int id)
    {
        StressTestController ctrl = StressTestController.Instance;
        if (ctrl == null)
        {
            GUILayout.Label("StressTestController not found.");
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            return;
        }

        GUILayout.BeginVertical();

        ctrl.enableStressTest = GUILayout.Toggle(ctrl.enableStressTest, "Enable Stress Test (Online Only)");

        GUI.enabled = ctrl.enableStressTest;

        GUILayout.Space(6);
        GUILayout.Label("Presets");
        int newPresetIndex = GUILayout.SelectionGrid(presetIndex, PresetNames, 1);
        if (newPresetIndex != presetIndex)
        {
            presetIndex = newPresetIndex;
            ApplyPreset(ctrl, presetIndex);
        }

        GUILayout.Space(6);
        GUILayout.Label("Network Chaos");
        ctrl.enableNetworkChaos = GUILayout.Toggle(ctrl.enableNetworkChaos, "Enable Network Chaos");
        ctrl.affectOutbound = GUILayout.Toggle(ctrl.affectOutbound, "Affect Outbound (their view)");
        ctrl.affectInbound = GUILayout.Toggle(ctrl.affectInbound, "Affect Inbound (your view)");
        ctrl.outboundLossChance = DrawFloat01("Outbound Loss", ctrl.outboundLossChance);
        ctrl.inboundLossChance = DrawFloat01("Inbound Loss", ctrl.inboundLossChance);
        ctrl.minLatencyMs = DrawInt("Min Latency (ms)", ctrl.minLatencyMs, 0, 500);
        ctrl.maxLatencyMs = DrawInt("Max Latency (ms)", ctrl.maxLatencyMs, ctrl.minLatencyMs, 1000);
        ctrl.jitterMs = DrawInt("Jitter (ms)", ctrl.jitterMs, 0, 250);
        ctrl.reorderChance = DrawFloat01("Reorder Chance", ctrl.reorderChance);
        ctrl.chaosSeed = DrawInt("Chaos Seed", ctrl.chaosSeed, 0, int.MaxValue);

        GUILayout.Space(6);
        GUILayout.Label("Deterministic Input");
        ctrl.enableDeterministicInput = GUILayout.Toggle(ctrl.enableDeterministicInput, "Enable Deterministic Input");
        ctrl.inputSeed = DrawInt("Input Seed", ctrl.inputSeed, 0, int.MaxValue);
        ctrl.directionChangeInterval = DrawInt("Dir Change Interval", ctrl.directionChangeInterval, 1, 120);
        ctrl.buttonPulseInterval = DrawInt("Button Pulse Interval", ctrl.buttonPulseInterval, 1, 120);
        ctrl.buttonHoldFrames = DrawInt("Button Hold Frames", ctrl.buttonHoldFrames, 1, 30);

        GUILayout.Space(6);
        GUILayout.Label("State Hashing");
        ctrl.enableStateHashing = GUILayout.Toggle(ctrl.enableStateHashing, "Enable State Hashing");
        ctrl.hashSendIntervalFrames = DrawInt("Hash Interval (frames)", ctrl.hashSendIntervalFrames, 1, 120);
        ctrl.dumpStateOnMismatch = GUILayout.Toggle(ctrl.dumpStateOnMismatch, "Dump State On Mismatch");

        GUILayout.Space(8);
        if (GUILayout.Button("Reset Stress Test State"))
        {
            ctrl.ResetForNewMatch();
        }

        if (GUILayout.Button("Log Dump Folder Path"))
        {
            string path = ctrl.GetDumpDirectory();
            Debug.Log($"[StressTest] Desync dump folder: {path}");
        }

        GUI.enabled = true;
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private void ApplyPreset(StressTestController ctrl, int index)
    {
        switch (index)
        {
            case 0: // None
                ctrl.enableNetworkChaos = false;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0f;
                ctrl.inboundLossChance = 0f;
                ctrl.minLatencyMs = 0;
                ctrl.maxLatencyMs = 0;
                ctrl.jitterMs = 0;
                ctrl.reorderChance = 0f;
                break;
            case 1: // Light
                ctrl.enableNetworkChaos = true;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0.01f;
                ctrl.inboundLossChance = 0.01f;
                ctrl.minLatencyMs = 60;
                ctrl.maxLatencyMs = 90;
                ctrl.jitterMs = 10;
                ctrl.reorderChance = 0f;
                break;
            case 2: // Medium
                ctrl.enableNetworkChaos = true;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0.03f;
                ctrl.inboundLossChance = 0.03f;
                ctrl.minLatencyMs = 80;
                ctrl.maxLatencyMs = 140;
                ctrl.jitterMs = 20;
                ctrl.reorderChance = 0.01f;
                break;
            case 3: // Heavy
                ctrl.enableNetworkChaos = true;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0.05f;
                ctrl.inboundLossChance = 0.05f;
                ctrl.minLatencyMs = 120;
                ctrl.maxLatencyMs = 200;
                ctrl.jitterMs = 30;
                ctrl.reorderChance = 0.02f;
                break;
            case 4: // Jitter
                ctrl.enableNetworkChaos = true;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0.02f;
                ctrl.inboundLossChance = 0.02f;
                ctrl.minLatencyMs = 0;
                ctrl.maxLatencyMs = 60;
                ctrl.jitterMs = 60;
                ctrl.reorderChance = 0f;
                break;
            case 5: // Reorder
                ctrl.enableNetworkChaos = true;
                ctrl.affectOutbound = true;
                ctrl.affectInbound = true;
                ctrl.outboundLossChance = 0.01f;
                ctrl.inboundLossChance = 0.01f;
                ctrl.minLatencyMs = 40;
                ctrl.maxLatencyMs = 100;
                ctrl.jitterMs = 15;
                ctrl.reorderChance = 0.02f;
                break;
            default:
                break;
        }
    }

    private float DrawFloat01(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(170));
        string text = GUILayout.TextField(value.ToString("0.00", CultureInfo.InvariantCulture), GUILayout.Width(60));
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            value = Mathf.Clamp01(parsed);
        }
        GUILayout.EndHorizontal();
        return value;
    }

    private int DrawInt(string label, int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(170));
        string text = GUILayout.TextField(value.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60));
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            value = Mathf.Clamp(parsed, min, max);
        }
        GUILayout.EndHorizontal();
        return value;
    }
}

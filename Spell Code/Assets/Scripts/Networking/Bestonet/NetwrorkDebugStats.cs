using UnityEngine;

public class NetworkDebugStats : MonoBehaviour
{
    // Toggle this on/off in the inspector
    public bool showDebugUI = true;

    private void OnGUI()
    {
        if (!showDebugUI) return;

        if (MatchMessageManager.Instance == null || RollbackManager.Instance == null) return;

        // Define box style
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        // Calculate box height based on lines
        float width = 250;
        float height = 130;
        float margin = 10;

        GUILayout.BeginArea(new Rect(Screen.width - width - margin, margin, width, height), "Network Stats", style);
        GUILayout.Space(25); // Space for title

        // --- PING ---
        int ping = MatchMessageManager.Instance.Ping;
        GUI.color = ping < 100 ? Color.green : (ping < 200 ? Color.yellow : Color.red);
        GUILayout.Label($"Ping: {ping}ms");

        // --- ROLLBACK INFO ---
        GUI.color = Color.white;
        int rbFrames = RollbackManager.Instance.RollbackFrames;
        if (rbFrames > 0) GUI.color = Color.red; // Flash red if rolling back
        GUILayout.Label($"Last Rollback: {rbFrames} frames");

        // --- FRAME SYNC ---
        GUI.color = Color.white;
        int frameAdv = RollbackManager.Instance.localFrameAdvantage;
        GUILayout.Label($"Frame Advantage: {frameAdv}");

        GUILayout.Label($"Local Frame: {RollbackManager.Instance.localFrame}");
        GUILayout.Label($"Remote Frame (Est): {RollbackManager.Instance.predictedRemoteFrame}");

        GUILayout.EndArea();
    }
}
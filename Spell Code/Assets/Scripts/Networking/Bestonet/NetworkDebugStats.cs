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
        style.normal.textColor = GameManager.colors["white"];

        // Calculate box height based on lines
        float width = 250;
        float height = 130;
        float margin = 10;

        GUILayout.BeginArea(new Rect(Screen.width - width - margin, margin, width, height), "Network Stats", style);
        GUILayout.Space(25); // Space for title

        // --- PING ---
        int ping = MatchMessageManager.Instance.Ping;
        GUI.color = ping < 100 ? GameManager.colors["green"] : (ping < 200 ? GameManager.colors["yellow"] : GameManager.colors["red"]);
        GUILayout.Label($"Ping: {ping}ms");

        // --- ROLLBACK INFO ---
        GUI.color = GameManager.colors["white"];
        int rbFrames = RollbackManager.Instance.RollbackFrames;
        if (rbFrames > 0) GUI.color = GameManager.colors["red"]; // Flash red if rolling back
        GUILayout.Label($"Last Rollback: {rbFrames} frames");

        // --- FRAME SYNC ---
        GUI.color = GameManager.colors["white"];
        int frameAdv = RollbackManager.Instance.localFrameAdvantage;
        GUILayout.Label($"Frame Advantage: {frameAdv}");

        GUILayout.Label($"Local Frame: {RollbackManager.Instance.localFrame}");
        GUILayout.Label($"Remote Frame (Est): {RollbackManager.Instance.predictedRemoteFrame}");

        GUILayout.EndArea();
    }
}
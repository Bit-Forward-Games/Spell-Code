using UnityEngine;
using TMPro; // Make sure to add this for TextMeshPro components
using Steamworks; // Make sure to add this for SteamId
using System;

public class TempConnectionUI : MonoBehaviour
{
    public TMP_InputField opponentIdField;
    public TextMeshProUGUI myIdText;
    public GameObject connectionUI; // Assign the parent UI object (Canvas, Panel, etc.)

    void Start()
    {
        // Make sure Steam is running
        if (!SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running! Online test will fail.");
            myIdText.text = "Error: Steam is not running.";
            return;
        }

        // Display your own Steam ID so the other player can copy it
        myIdText.text = $"My Steam ID: {SteamClient.SteamId.Value}";
    }

    // Link this method to your Button's OnClick() event in the Inspector
    public void OnConnectClicked()
    {
        string idString = opponentIdField.text.Trim();
        if (string.IsNullOrEmpty(idString))
        {
            Debug.LogError("Opponent Steam ID field is empty.");
            return;
        }

        try
        {
            // Parse the ID from the input field
            SteamId opponentSteamId = ulong.Parse(idString);
            if (!opponentSteamId.IsValid)
            {
                Debug.LogError("The entered Steam ID is not valid.");
                return;
            }

            Debug.Log($"Attempting to connect to opponent: {opponentSteamId.Value}");

            // --- THIS IS THE KEY ---
            // Tell GameManager and MatchMessageManager to start the online match.
            // We'll hardcode Player 0 vs Player 1 for this test.
            // You'll need a way to decide who is P0 and P1 (e.g., two buttons: "Host as P0" / "Connect as P1")

            // For a simple test: Assume the person clicking this button *first* is P0 (Host)
            // This is just an example; a better test would have two buttons.

            // Simple Two-Button Setup (Recommended):
            // Create two buttons: "Host (P0)" and "Connect (P1)"
            // Host button: Calls StartMatch(0, 1, opponentSteamId)
            // Connect button: Calls StartMatch(1, 0, opponentSteamId)

            // --- Using one button (simple, but both players must coordinate) ---
            // Let's assume for this test:
            // Editor = Player 0
            // Build = Player 1
            // You'll need to know which one you are.

            int localIdx = 0;
            int remoteIdx = 1;

            // A simple way to check if we are the build (and thus Player 1)
#if !UNITY_EDITOR
            localIdx = 1;
            remoteIdx = 0;
#endif

            GameManager.Instance.StartOnlineMatch(localIdx, remoteIdx, opponentSteamId);
            MatchMessageManager.Instance.StartMatch(opponentSteamId);

            // Hide the connection UI
            if (connectionUI != null)
            {
                connectionUI.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse Steam ID or start match: {e.Message}");
        }
    }
}
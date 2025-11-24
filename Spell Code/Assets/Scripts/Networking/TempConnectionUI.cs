using UnityEngine;
using TMPro; 
using Steamworks;
using System;
using UnityEngine.SceneManagement;

public class TempConnectionUI : MonoBehaviour
{
    public TMP_InputField opponentIdField;
    public TextMeshProUGUI myIdText;
    public GameObject onlineUI;

    [Header("Scene Setup")]
    public string gameplaySceneName = "MainMenu";

    void Start()
    {
        // Make sure Steam is running
        if (!SteamClient.IsValid)
        {
            Debug.LogError("Steam is not running! Online test will fail.");
            myIdText.text = "Error: Steam is not running.";
            return;
        }

        // Display own Steam ID
        if (myIdText != null)
        {
            myIdText.text = $"My Steam ID: {SteamClient.SteamId.Value}";
        }
    }

    // --- Helper Method to Parse ID and Start Match ---
    private void AttemptConnection(int localPlayerIndex, int remotePlayerIndex)
    {
        if (opponentIdField == null)
        {
            Debug.LogError("Opponent ID Field is not assigned in the Inspector.");
            return;
        }

        string idString = opponentIdField.text.Trim();
        if (string.IsNullOrEmpty(idString))
        {
            Debug.LogError("Opponent Steam ID field is empty.");
            return;
        }

        try
        {
            // Parse the ID
            SteamId opponentSteamId = ulong.Parse(idString);
            if (!opponentSteamId.IsValid)
            {
                Debug.LogError("The entered Steam ID is not valid.");
                return;
            }
            if (opponentSteamId == SteamClient.SteamId)
            {
                Debug.LogError("Cannot connect to yourself! Enter the opponent's Steam ID.");
                return;
            }


            Debug.Log($"Attempting connection. Local Player: {localPlayerIndex}, Opponent: {opponentSteamId.Value}");

            // --- Start Managers ---
            // Ensure instances exist before calling
            if (GameManager.Instance == null || MatchMessageManager.Instance == null)
            {
                Debug.LogError("GameManager or MatchMessageManager instance not found!");
                return;
            }

            //GameManager.Instance.StartOnlineMatch(localPlayerIndex, remotePlayerIndex, opponentSteamId);
            try
            {
                GameManager.Instance.StartOnlineMatch(localPlayerIndex, remotePlayerIndex, opponentSteamId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in StartOnlineMatch: {ex}");
            }
            Debug.Log("About to call MatchMessageManager.Instance.StartMatch");
            MatchMessageManager.Instance.StartMatch(opponentSteamId);
            Debug.Log("Called MatchMessageManager.Instance.StartMatch");

            // --- End Start Managers ---


            // --- Hide UI & Load Scene ---
            if (onlineUI != null)
            {
                onlineUI.SetActive(false);
            }
            else
            {
                Debug.LogWarning("Connection UI Parent not assigned. UI will not be hidden automatically.");
            }

            // Load the gameplay scene
            if (!string.IsNullOrEmpty(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                Debug.LogError("Gameplay Scene Name is not set in the Inspector.");
            }
            // --- End Hide UI & Load Scene ---

        }
        catch (FormatException)
        {
            Debug.LogError($"Invalid Steam ID format entered: '{idString}'. Please enter a valid 17-digit number.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse Steam ID or start match: {e.Message}");
        }
    }


    // --- Button Click Handlers ---

    /// <summary>
    /// Call this method from the "Host Game (P0)" button's OnClick event.
    /// </summary>
    public void OnHostClicked()
    {
        Debug.Log("Host Game (P0) button clicked.");
        AttemptConnection(localPlayerIndex: 0, remotePlayerIndex: 1);
    }

    /// <summary>
    /// Call this method from the "Join Game (P1)" button's OnClick event.
    /// </summary>
    public void OnJoinClicked()
    {
        Debug.Log("Join Game (P1) button clicked.");
        AttemptConnection(localPlayerIndex: 1, remotePlayerIndex: 0);
    }
}
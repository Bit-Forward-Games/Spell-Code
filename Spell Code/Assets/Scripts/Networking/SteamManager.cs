using UnityEngine;
using Steamworks;
using System; // Needed for Exception

// Makes sure this script runs before others, especially TempConnectionUI
[DefaultExecutionOrder(-100)]
public class SteamManager : MonoBehaviour
{
    // Use 480 for testing/development if you don't have your own App ID yet
    // Replace with your actual App ID for release builds.
    private const uint SteamAppId = 480;

    private static SteamManager instance;

    void Awake()
    {
        // Singleton pattern to prevent multiple managers
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // Keep it running between scenes

        // --- Initialize Steamworks ---
        try
        {
            // Try initializing using the App ID
            SteamClient.Init(SteamAppId, true); // true for async callbacks

            SteamNetworking.AllowP2PPacketRelay(true);

            // Set the verbosity level (Msg = normal info, Verbose = everything)
            SteamNetworkingUtils.DebugLevel = NetDebugOutput.Msg;

            // Subscribe to the debug event
            SteamNetworkingUtils.OnDebugOutput += (type, message) =>
            {
                Debug.Log($"[Steam Net] {type}: {message}");
            };

            SteamNetworking.OnP2PSessionRequest += (steamId) =>
            {
                Debug.Log($"[P2P] Incoming connection request from {steamId}");
                SteamNetworking.AcceptP2PSessionWithUser(steamId);
            };

            SteamNetworking.OnP2PConnectionFailed += (steamId, error) =>
            {
                Debug.LogError($"[P2P] Connection failed with {steamId}: {error}");
            };

            if (!SteamClient.IsValid)
            {
                Debug.LogError("Steamworks initialization failed. Steam might not be running or steam_appid.txt might be missing/incorrect.");
                // Optionally quit the application or disable online features
                // Application.Quit();
            }
            else
            {
                //Debug.Log($"Steamworks Initialized! AppId: {SteamClient.AppId}, User: {SteamClient.Name} ({SteamClient.SteamId})");
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"Steamworks initialization exception: {e.Message}");
            // Handle exceptions (e.g., Steam not running, DLL issues)
        }
        // --- End Initialization ---
    }

    void Update()
    {
        // Run Steam callbacks every frame - VERY IMPORTANT!
        if (SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    void OnApplicationQuit()
    {
        // Shut down Steamworks when the game closes
        if (SteamClient.IsValid)
        {
            Debug.Log("Shutting down Steamworks...");
            SteamClient.Shutdown();
        }
    }
}
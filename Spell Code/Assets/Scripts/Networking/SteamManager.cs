using UnityEngine;
using Steamworks;
using System; // Needed for Exception

// Makes sure this script runs before others, especially TempConnectionUI
[DefaultExecutionOrder(-100)]
public class SteamManager : MonoBehaviour
{
    public const string DebugToolsBetaBranch = "4playersupporttesting";

#if STEAM_PLAYTEST
    // Playtest App ID
    private const uint SteamAppId = 4569980;
#else
    // Base Game App ID
    private const uint SteamAppId = 4500000;
#endif

    private static SteamManager instance;
    private bool hasShutDownSteam;
#if !UNITY_EDITOR
    private static bool debugToolsEnabled;
#endif

    /// <summary>
    /// Debug keyboard shortcuts are available in the Unity editor and on the dedicated
    /// private Steam beta branch, but never on the public/default Steam branch.
    /// </summary>
    public static bool DebugToolsEnabled
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return debugToolsEnabled;
#endif
        }
    }

    void Awake()
    {
#if UNITY_EDITOR
        enabled = false;
#else
// Singleton pattern to prevent multiple managers
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // Keep it running between scenes

        if (GetComponent<SteamLobbyManager>() == null)
        {
            gameObject.AddComponent<SteamLobbyManager>();
        }
        
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
                string currentBetaName = SteamApps.CurrentBetaName;
                debugToolsEnabled = string.Equals(
                    currentBetaName,
                    DebugToolsBetaBranch,
                    StringComparison.Ordinal);

                Debug.Log($"Steam beta branch: {currentBetaName ?? "public/default"}. Private debug tools enabled: {debugToolsEnabled}.");
                //Debug.Log($"Steamworks Initialized! AppId: {SteamClient.AppId}, User: {SteamClient.Name} ({SteamClient.SteamId})");
            }

        }
        catch (Exception e)
        {
            debugToolsEnabled = false;
            Debug.LogError($"Steamworks initialization exception: {e.Message}");
            // Handle exceptions (e.g., Steam not running, DLL issues)
        }
        // --- End Initialization ---
#endif
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
        ShutdownSteam();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            ShutdownSteam();
            instance = null;
        }
    }

    private void ShutdownSteam()
    {
        if (hasShutDownSteam)
        {
            return;
        }

        hasShutDownSteam = true;
#if !UNITY_EDITOR
        debugToolsEnabled = false;
#endif

        if (SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.Shutdown();
        }

        if (SteamClient.IsValid)
        {
            Debug.Log("Shutting down Steamworks...");
            SteamClient.Shutdown();
        }
    }
}

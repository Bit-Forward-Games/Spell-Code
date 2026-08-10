using UnityEngine;
using Steamworks;
using System; // Needed for Exception

// Makes sure this script runs before others, especially TempConnectionUI
[DefaultExecutionOrder(-100)]
public class SteamManager : MonoBehaviour
{
    public const string DebugToolsBetaBranch = "4playersupporttesting";
    public const string AdditionalDebugToolsBetaBranch = "testing";

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
            // Pump callbacks explicitly from Update so every Steam event is handled on Unity's
            // main thread. Facepunch's async callback worker must not run at the same time as our
            // manual RunCallbacks call: once networking/relay callbacks become active the two
            // pumps can race SteamClient.Shutdown and leave a standalone build hung while quitting.
            SteamClient.Init(SteamAppId, false);

            SteamNetworking.AllowP2PPacketRelay(true);

            // Pre-warm the relay network. Without this, Steam only fetches the relay config and
            // runs its ping measurement lazily on the first P2P connection, which costs several
            // seconds on a cold client and can push a joining peer past the connection timeout.
            SteamNetworkingUtils.InitRelayNetworkAccess();

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
                    StringComparison.Ordinal)
                    || string.Equals(
                        currentBetaName,
                        AdditionalDebugToolsBetaBranch,
                        StringComparison.Ordinal);

                Debug.Log($"Steam beta branch: {currentBetaName ?? "public/default"}. Private debug tools enabled: {debugToolsEnabled}.");
                //Debug.Log($"Steamworks Initialized! AppId: {SteamClient.AppId}, User: {SteamClient.Name} ({SteamClient.SteamId})");

                // Fired on every boot rather than off SettingsManager.IsFirstLaunch(): anyone
                // who played before achievements shipped already has firstLaunchComplete set
                // and would never earn this, and a wiped settings.json shouldn't re-arm it.
                // Steam no-ops an achievement the account already owns, so an existing player
                // is backfilled the next time they launch. The settings flag stays what it is
                // today -- the gate for the first-boot tutorial prompt.
                SteamAchievements.Unlock(SteamAchievements.FirstLaunch);
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

        // Retries any unlock that was requested before Steam (or the user's stats) was
        // ready. Self-guarding and a single lookup when nothing is queued.
        SteamAchievements.Pump();
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
            Debug.Log("Steamworks shutdown complete.");
        }
    }
}

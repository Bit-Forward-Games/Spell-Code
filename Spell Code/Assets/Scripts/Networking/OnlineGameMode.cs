using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game mode the host picked for an online match, as it travels between peers.
///
/// The modes themselves are authored in the scene ("Multiplayer Gamemodes Panel 2") rather than
/// hardcoded here: each mode button carries an <see cref="OnlineGameModeOption"/> holding a
/// wire-stable <see cref="Id"/> and the <see cref="DisplayName"/> shown in the Friends Lobby's
/// "Selected GameMode" label. Adding or renaming a mode is then pure Inspector work.
///
/// How the choice stays in sync: the host publishes the id AND the display name into Steam lobby
/// data, every member reads the same values back out of that lobby, and each one calls
/// <see cref="GameManager.ApplyOnlineGameMode"/> immediately before the match starts. Nothing about
/// the mode touches the netcode wire format, so no serializer change and no state-hash risk.
///
/// NOTE: nothing in the simulation reads a mode yet, so every mode currently plays identically.
/// Making them differ needs the two RAM-target sites reconciled first -- GameManager.OnSceneLoaded
/// uses baseRamNeeddedtowin(400) + 100/round while ApplyOnlineTotalRoundsPlayed hardcodes 300 +
/// 100/round. Wire a rule at only one of them and the mode applies for round 1 then evaporates.
/// </summary>
public struct OnlineGameModeSelection
{
    /// <summary>Fallback mode. Matches the text the Friends Lobby prefab ships with.</summary>
    public const string DefaultId = "normal";
    public const string DefaultDisplayName = "Normal mode";

    /// <summary>Wire-stable identifier. Published to Steam lobby data; keep it stable across builds.</summary>
    public string Id;

    /// <summary>Human-readable label for the "Selected GameMode" field.</summary>
    public string DisplayName;

    public OnlineGameModeSelection(string id, string displayName)
    {
        Id = string.IsNullOrEmpty(id) ? DefaultId : id;
        DisplayName = string.IsNullOrEmpty(displayName) ? DefaultDisplayName : displayName;
    }

    public static OnlineGameModeSelection Default => new OnlineGameModeSelection(DefaultId, DefaultDisplayName);

    public bool IsValid => !string.IsNullOrEmpty(Id);

    /// <summary>
    /// Resolves whatever came out of lobby data into something safe to show and compare. A peer on a
    /// build that has never heard of the host's mode still gets a usable label rather than a blank
    /// field, and an empty id degrades to the default instead of splitting the peers.
    /// </summary>
    public static OnlineGameModeSelection Resolve(string id, string displayName)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Default;
        }

        if (string.IsNullOrEmpty(displayName))
        {
            // Fall back to a locally registered option with the same id before giving up on a label.
            OnlineGameModeOption known = OnlineGameModeRegistry.Find(id);
            displayName = known != null ? known.DisplayName : id;
        }

        return new OnlineGameModeSelection(id, displayName);
    }
}

/// <summary>
/// Tracks the <see cref="OnlineGameModeOption"/>s present in the scene, so a peer can turn a bare id
/// from lobby data back into a label and the lobby panel can pick a sensible opening mode without
/// anything hardcoding the list.
/// </summary>
public static class OnlineGameModeRegistry
{
    private static readonly List<OnlineGameModeOption> cache = new List<OnlineGameModeOption>();
    private static bool cacheValid;
    private static int nextRescanFrame;

    // The mode chooser panel ("Multiplayer Gamemodes Panel 2") starts INACTIVE, so its buttons never
    // run Awake or OnEnable and cannot register themselves. Discovery therefore has to sweep
    // inactive objects too. The result is cached because the lobby panel asks every frame; a rescan
    // only happens when the cache was invalidated or is still empty (nothing authored yet), and even
    // then no more than a few times a second.
    private const int EmptyRescanIntervalFrames = 120;

    public static IReadOnlyList<OnlineGameModeOption> All
    {
        get
        {
            EnsureCache();
            return cache;
        }
    }

    /// <summary>Forces the next lookup to re-sweep the scene. Cheap; call it freely.</summary>
    public static void Invalidate()
    {
        cacheValid = false;
        nextRescanFrame = 0;
    }

    private static void EnsureCache()
    {
        if (cacheValid)
        {
            // A scene load destroys the options; destroyed Objects compare equal to null.
            for (int i = 0; i < cache.Count; i++)
            {
                if (cache[i] == null)
                {
                    cacheValid = false;
                    break;
                }
            }
        }

        if (cacheValid)
        {
            return;
        }

        if (cache.Count == 0 && Time.frameCount < nextRescanFrame)
        {
            return; // nothing authored yet; don't sweep the scene every frame
        }

        cache.Clear();
        cache.AddRange(Object.FindObjectsByType<OnlineGameModeOption>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None));

        cacheValid = cache.Count > 0;
        nextRescanFrame = Time.frameCount + EmptyRescanIntervalFrames;
    }

    public static OnlineGameModeOption Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < All.Count; i++)
        {
            OnlineGameModeOption option = All[i];
            if (option != null && option.ModeId == id)
            {
                return option;
            }
        }

        return null;
    }

    /// <summary>First option authored in the scene, used as the lobby's opening pick.</summary>
    public static OnlineGameModeSelection FirstOrDefault()
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i] != null)
            {
                return All[i].Selection;
            }
        }

        return OnlineGameModeSelection.Default;
    }
}

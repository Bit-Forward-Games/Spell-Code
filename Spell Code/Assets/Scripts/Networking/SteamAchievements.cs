using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// Fire-and-forget wrapper over Steam achievements.
///
/// Static rather than a MonoBehaviour on purpose. ExecuteOrder66 destroys every
/// DontDestroyOnLoad root, SteamManager included, and that shuts Steam down until the
/// next scene's copy re-inits it. A component would lose its queue on every return to menu;
/// statics live for the process, so an unlock requested during that window still lands.
///
/// Unlock() is safe to call at any time, from anywhere, with Steam down, not yet
/// initialised, or with the user's stats not yet received. Anything that doesn't reach
/// Steam immediately is queued and retried from SteamManager.Update via Pump().
/// </summary>
public static class SteamAchievements
{
    // API Names
    // Must match the "API Name" column under Steamworks -> App Admin -> Stats &
    // Achievements character for character.
    // Steam rejects an unknown name silently, so a mismatch surfaces only as the
    // "giving up" warning in Pump().
    //
    // Achievements are configured per App ID, and SteamManager inits against the playtest
    // app under STEAM_PLAYTEST and the base app otherwise, every name here has to exist
    // on BOTH apps or unlocks silently vanish on whichever one is missing them.

    public const string FirstLaunch = "ACH_First_Launch";

    // One per unlockable spell, add as the spells land:
    

    // Retry policy

    // Trigger() calls StoreStats internally and Valve rate limits that call, so retries
    // are throttled rather than run per-frame.
    private const float RetryIntervalSeconds = 2f;

    // Only counts attempts Steam actively refused (see Pump), so this is ~20s of a live
    // Steam saying no, far longer than a cold stats fetch takes, and in practice it
    // means the name isn't published on this App ID.
    private const int MaxAttempts = 10;

    // State

    // API name -> attempts already refused by a live Steam.
    private static readonly Dictionary<string, int> pending = new Dictionary<string, int>();

    // Reused so the retry sweep can mutate `pending` without allocating or invalidating
    // an enumerator mid-iteration.
    private static readonly List<string> retryScratch = new List<string>();

    // Compared against unscaled time: menus and pauses set Time.timeScale to 0, and a
    // queued unlock must not stall for as long as the player sits in one.
    private static float nextRetryTime;

    /// <summary>
    /// Request an achievement unlock. Never throws, never blocks, and is cheap to call
    /// repeatedly, an achievement the account already owns is a no-op.
    ///
    /// Do NOT call this from inside rollback resimulation. Gameplay call sites want either
    /// UnlockForPlayer below, or a SimGuards.IsLocalRealFrame check of their own.
    /// </summary>
    public static void Unlock(string apiName)
    {
        if (string.IsNullOrEmpty(apiName) || pending.ContainsKey(apiName))
        {
            return;
        }

        if (!TryTrigger(apiName))
        {
            pending[apiName] = 0;
        }
    }

    /// <summary>
    /// Unlock on behalf of the player in the specific player slot, for call sites that
    /// live inside the deterministic sim. That code runs again on every resim pass, and runs
    /// for every player on every peer's machine, so a bare Unlock() there would fire
    /// repeatedly and credit you for other people's play. Drops the request unless this is a
    /// real frame and the slot belongs to the player at this keyboard.
    ///
    /// Use this for feats that fire straight off a gameplay event. Anything with a save
    /// behind it should gate the save and the unlock together with one SimGuards call, and
    /// then use plain Unlock() here.
    /// </summary>
    public static void UnlockForPlayer(int playerSlot, string apiName)
    {
        if (!SimGuards.IsLocalRealFrame(playerSlot))
        {
            return;
        }

        Unlock(apiName);
    }

    /// <summary>
    /// Drives the retry queue. Called every frame from SteamManager.Update.
    /// </summary>
    public static void Pump()
    {
        // Empty is the case on virtually every frame of the game's life; keep it a single
        // lookup. Steam not being up yet doesn't burn an attempt -- only Steam being up
        // and refusing the name does.
        if (pending.Count == 0 || !SteamClient.IsValid || Time.unscaledTime < nextRetryTime)
        {
            return;
        }

        nextRetryTime = Time.unscaledTime + RetryIntervalSeconds;

        retryScratch.Clear();
        retryScratch.AddRange(pending.Keys);

        for (int i = 0; i < retryScratch.Count; i++)
        {
            string apiName = retryScratch[i];

            if (TryTrigger(apiName))
            {
                pending.Remove(apiName);
                continue;
            }

            int attempts = pending[apiName] + 1;
            if (attempts >= MaxAttempts)
            {
                Debug.LogWarning(
                    $"[Achievements] Giving up on '{apiName}' after {attempts} attempts. "
                    + $"Check that API name is published for app {SteamClient.AppId}.");
                pending.Remove(apiName);
            }
            else
            {
                pending[apiName] = attempts;
            }
        }
    }

    /// <summary>
    /// Returns true when the achievement is settled (unlocked now, or already owned) and
    /// false when it should be retried later.
    /// </summary>
    private static bool TryTrigger(string apiName)
    {
        // Also what makes this class inert in the editor: SteamManager disables itself and
        // never calls SteamClient.Init there.
        if (!SteamClient.IsValid)
        {
            return false;
        }

        try
        {
            Achievement achievement = new Achievement(apiName);

            // Already on the account, possibly from an earlier session.
            if (achievement.State)
            {
                return true;
            }

            // Sets the achievement and calls StoreStats, which is what commits it and pops
            // the overlay toast. False generally means the user's stats haven't arrived.
            if (!achievement.Trigger())
            {
                return false;
            }

            Debug.Log($"[Achievements] Unlocked '{apiName}'.");
            return true;
        }
        catch (Exception e)
        {
            // Hard guard so an achievement can never take down a frame -- Facepunch throws
            // if Steam is torn down between the IsValid check above and the call itself,
            // which ExecuteOrder66 makes a real possibility.
            Debug.LogWarning($"[Achievements] '{apiName}' failed: {e.Message}");
            return false;
        }
    }
}

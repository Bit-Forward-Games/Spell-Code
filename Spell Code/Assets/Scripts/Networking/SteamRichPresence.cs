using Steamworks;
using UnityEngine;

/// <summary>
/// Publishes the status line Steam shows under the game's name in a friend's friends list.
///
/// Two halves have to line up before anything appears. This file sets the magic "steam_display"
/// key to a LOCALIZATION TOKEN; the text that token renders as is authored on the app's Rich
/// Presence Localization page on the Steamworks partner site and has to be published there. An
/// unpublished or misspelled token renders as a blank line rather than an error, so a missing
/// status is almost always the token rather than this code.
///
/// Tokens are configured per App ID, and SteamManager inits against the playtest app under
/// STEAM_PLAYTEST and the base app otherwise, so every token below has to exist on BOTH apps or
/// the status silently vanishes on whichever one is missing it (the same trap SteamAchievements
/// documents for achievement API names).
///
/// Static for the same reason SteamAchievements is: ExecuteOrder66 destroys every
/// DontDestroyOnLoad root, SteamManager included, which shuts Steam down until the next scene's
/// copy re-inits it. The published-token cache is dropped across that gap so the status is
/// republished against the fresh Steam session instead of being suppressed as unchanged.
/// </summary>
public static class SteamRichPresence
{
    // Token names, leading '#' included. These are the strings to publish on the partner site.
    // Friends and LocalPlay are separate tokens so the two can be worded differently later, point
    // them at identical text there if both should read the same.
    public const string TokenFriends = "#Status_Friends";
    public const string TokenLocalPlay = "#Status_LocalPlay";
    public const string TokenMatchmaking = "#Status_Matchmaking";

    // The only key Steam actually renders. Any other key exists purely to be interpolated into the
    // token's text as %key%, so a status with no substitutions needs this one alone.
    private const string DisplayKey = "steam_display";

    // Resolving the status reads live lobby state, and IsInPartyLobby costs a native GetData call.
    // Twice a second is far quicker than anyone reads a friends list.
    private const float PumpIntervalSeconds = 0.5f;

    // Null means nothing is published, which is also how the menus state is represented.
    private static string publishedToken;
    private static bool steamWasValid;
    // Unscaled: menus and pauses sit at timeScale 0.
    private static float nextPumpTime;

    /// <summary>
    /// Called every frame from SteamManager.Update. Self throttling and safe to call with Steam
    /// down, not yet initialised, or mid teardown.
    /// </summary>
    public static void Pump()
    {
        if (!SteamClient.IsValid)
        {
            steamWasValid = false;
            publishedToken = null;
            return;
        }

        if (!steamWasValid)
        {
            // Fresh Steam session. Nothing this class published before the restart survived, so
            // forget the cache and let the next resolve republish.
            steamWasValid = true;
            publishedToken = null;
            nextPumpTime = 0f;
        }

        if (Time.unscaledTime < nextPumpTime)
        {
            return;
        }

        nextPumpTime = Time.unscaledTime + PumpIntervalSeconds;
        Apply(ResolveToken());
    }

    /// <summary>Wipes the status line. Steam does this on exit anyway, this is for explicit resets.</summary>
    public static void Clear()
    {
        if (!SteamClient.IsValid)
        {
            return;
        }

        publishedToken = null;
        SteamFriends.ClearRichPresence();
    }

    /// <summary>
    /// The token for the player's current activity, or null to show no status line at all.
    ///
    /// Order matters. A live match is checked first because IsSearchingForMatch and IsInPartyLobby
    /// can still read true on the frames either side of a match starting, and the match is the
    /// more truthful answer during that overlap.
    /// </summary>
    private static string ResolveToken()
    {
        GameManager gameManager = GameManager.Instance;
        SteamLobbyManager lobby = SteamLobbyManager.Instance;

        if (gameManager != null && gameManager.isOnlineMatchActive)
        {
            // Matchmaking is the only origin that isn't someone the player chose to play with. The
            // legacy host+invite lobby latches None but is still an invited friend, so it falls
            // through to the friends line rather than being treated as matchmaking.
            return SteamLobbyManager.ActiveMatchOrigin == SteamLobbyManager.OnlineMatchOrigin.Matchmaking
                ? TokenMatchmaking
                : TokenFriends;
        }

        if (lobby != null)
        {
            // Pre-match online states, so the status is right while sitting in a lobby rather than
            // only once the match arms (ActiveMatchOrigin doesn't latch until then).
            if (lobby.IsSearchingForMatch)
            {
                return TokenMatchmaking;
            }

            if (lobby.IsInPartyLobby)
            {
                return TokenFriends;
            }
        }

        // Offline. Two or more players on the one machine is Local Play; a single player is the
        // solo lobby, the tutorial or the training grounds, which all read as idling in the menus.
        if (gameManager != null && gameManager.playerCount >= 2)
        {
            return TokenLocalPlay;
        }

        return null;
    }

    private static void Apply(string token)
    {
        if (token == publishedToken)
        {
            return;
        }

        publishedToken = token;

        if (string.IsNullOrEmpty(token))
        {
            // Menus: no second line at all, which is how a game sitting idle normally reads.
            SteamFriends.ClearRichPresence();
            return;
        }

        SteamFriends.SetRichPresence(DisplayKey, token);

        if (SteamManager.DebugToolsEnabled)
        {
            Debug.Log($"[RichPresence] steam_display='{token}'.");
        }
    }
}

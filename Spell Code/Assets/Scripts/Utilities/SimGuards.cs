/// <summary>
/// Gates for side effects triggered from inside the deterministic simulation that must not
/// be part of it: achievements, progression saves, analytics, anything reaching outside the
/// match.
///
/// The sim has two properties that make a naive side effect wrong. It re-runs frames that
/// already ran whenever rollback corrects a misprediction, so one logical event can execute
/// the same line many times. And online it simulates every player on every peer's machine,
/// so each machine sees every player's event as though it were local.
///
/// Nothing here may be read by code that affects the sim itself. These report local,
/// machine-specific state; branching the simulation on them desyncs the match.
/// </summary>
public static class SimGuards
{
    /// <summary>
    /// True when this frame is being simulated for the first time, false while rollback is
    /// replaying frames that already ran.
    ///
    /// Note this means "not a replay", not "confirmed". A live frame still runs on predicted
    /// remote input, so its outcome can be revised by a later correction. Side effects that
    /// cannot be taken back (an achievement, a purchase) are better triggered from somewhere
    /// already settled, such as a progression save or the end-of-match screen.
    /// </summary>
    public static bool IsRealFrame()
    {
        RollbackManager rollback = RollbackManager.Instance;

        // No manager means nothing is resimulating, so the frame is real by definition.
        return rollback == null || !rollback.isRollbackFrame;
    }

    /// <summary>
    /// True when <paramref name="playerSlot"/> belongs to a player at this keyboard.
    ///
    /// Offline this is true for every slot: local play is one machine on one account, and
    /// localPlayerIndex sits at its default of 0 there, so filtering by it would deny
    /// P2 through P4.
    /// </summary>
    public static bool IsLocalSlot(int playerSlot)
    {
        GameManager manager = GameManager.Instance;

        // A null manager means no match context at all, which behaves like offline.
        if (manager == null || !manager.isOnlineMatchActive)
        {
            return true;
        }

        return playerSlot == manager.localPlayerIndex;
    }

    /// <summary>
    /// The usual gate: run this side effect once, on the machine of the player it belongs to.
    /// </summary>
    public static bool IsLocalRealFrame(int playerSlot)
    {
        return IsRealFrame() && IsLocalSlot(playerSlot);
    }
}

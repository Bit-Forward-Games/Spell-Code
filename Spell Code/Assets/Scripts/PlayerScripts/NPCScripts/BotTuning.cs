/// <summary>
/// What each difficulty tier actually changes, kept as one table for the same reason SpellTactics
/// is: tuning is far easier to judge when every tier sits next to the others.
///
/// Nothing in here touches damage, speed or health. A bot that hits harder is not a harder
/// opponent, it is the same opponent with a bigger number -- and a player can feel the difference.
/// Every knob below is a way of playing worse or better.
/// </summary>
public readonly struct BotTuning
{
    /// <summary>
    /// Frames the bot's view of the world is allowed to go stale. This is the honest version of a
    /// slow opponent: it acts on where things *were*, so it whiffs and gets caught out.
    /// </summary>
    public readonly int ReactionFrames;

    /// <summary>
    /// Punk input mode. For a human it's an accessibility win -- one direction fires one of your
    /// first four actives instead of typing a code. A bot gets nothing from that, since it can
    /// enter any code perfectly, while still paying Punk's costs: cooldown becomes
    /// cooldown + codeLength * 30, and the spell list caps at four actives and two passives.
    /// That makes it a real, visible handicap rather than an invisible nerf.
    /// </summary>
    public readonly bool PunkMode;

    /// <summary>
    /// Multiplies the room a code needs before the bot commits. Below 1 is reckless -- it starts
    /// long codes at ranges where it gets punished for them, which is what makes an easy bot
    /// beatable in a way a player can actually exploit.
    /// </summary>
    public readonly float CommitmentSpaceScale;

    /// <summary>Gap between attack attempts.</summary>
    public readonly int FramesBetweenAttempts;

    /// <summary>
    /// Whether the bot reaches for the right tool or just whatever happens to be off cooldown.
    /// Deliberately a switch rather than a dice roll: "it threw the wrong spell" reads as a
    /// difficulty, where random choice just reads as inconsistency.
    /// </summary>
    public readonly bool PicksBestSpell;

    private BotTuning(
        int reactionFrames,
        bool punkMode,
        float commitmentSpaceScale,
        int framesBetweenAttempts,
        bool picksBestSpell)
    {
        ReactionFrames = reactionFrames;
        PunkMode = punkMode;
        CommitmentSpaceScale = commitmentSpaceScale;
        FramesBetweenAttempts = framesBetweenAttempts;
        PicksBestSpell = picksBestSpell;
    }

    public static BotTuning For(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            // Slow to notice things, stuck in Punk, throws codes it can't afford, and reaches for
            // whatever is ready rather than what the moment wants.
            case BotDifficulty.Easy:
                return new BotTuning(16, true, 0.6f, 48, false);

            // Reads the situation as it happens, picks its tool, and only commits with room.
            case BotDifficulty.Hard:
                return new BotTuning(0, false, 1.25f, 14, true);

            default:
                return new BotTuning(6, false, 1f, 24, true);
        }
    }
}

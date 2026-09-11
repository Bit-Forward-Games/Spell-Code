using System.Collections.Generic;

/// <summary>
/// Roughly how far a spell reaches, which is the main axis a bot picks on.
/// </summary>
public enum SpellRange
{
    Short,
    Medium,
    Long
}

/// <summary>
/// What a spell is for, which decides whether reaching for it makes sense at all.
/// </summary>
public enum SpellRole
{
    /// <summary>Aimed at the opponent right now.</summary>
    Attack,

    /// <summary>Buffs the owner or their basic attack. Worth casting from safety, never as a poke.</summary>
    Enhance,

    /// <summary>Placed on the stage and pays off later: traps, lingering fields, delayed blasts.</summary>
    Zone,

    /// <summary>Defensive or positional. Reactive, not something to open with.</summary>
    Utility
}

/// <summary>
/// AI-only tactical hints about each active spell, kept deliberately out of SpellData: none of this
/// affects gameplay, and one table is far easier to review and tune than the same facts scattered
/// across 38 constructors.
///
/// Values are taken from each spell's own player-facing description, which already states range in
/// the author's words for about half the list and describes the shape clearly for the rest. A spell
/// missing from this table still works -- it just falls back to a middling attack profile, so
/// adding a spell without touching this file degrades gracefully rather than breaking the bot.
/// </summary>
public static class SpellTactics
{
    public readonly struct Profile
    {
        public readonly SpellRange Range;
        public readonly SpellRole Role;

        /// <summary>Reaches meaningfully above the caster, so it answers an airborne opponent.</summary>
        public readonly bool HitsAbove;

        public Profile(SpellRange range, SpellRole role, bool hitsAbove = false)
        {
            Range = range;
            Role = role;
            HitsAbove = hitsAbove;
        }
    }

    private static readonly Profile Fallback = new Profile(SpellRange.Medium, SpellRole.Attack);

    private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
    {
        // --- Short range ---
        { "Abaddon Uppercut",     new Profile(SpellRange.Short,  SpellRole.Attack, hitsAbove: true) },
        { "Amon Slash",           new Profile(SpellRange.Short,  SpellRole.Attack) },
        { "Blade Of Ares",        new Profile(SpellRange.Short,  SpellRole.Attack) },
        { "Brimstone Cyclone Kick", new Profile(SpellRange.Short, SpellRole.Attack, hitsAbove: true) },
        { "Chains Of Thanatos",   new Profile(SpellRange.Short,  SpellRole.Attack) },
        { "Touch Of Midas",       new Profile(SpellRange.Short,  SpellRole.Attack) },
        { "Use The Card",         new Profile(SpellRange.Short,  SpellRole.Attack) },

        // --- Medium range ---
        { "Asuran Blades",        new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Bifrons Blade",        new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Get A Job",            new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Might Of Zeus",        new Profile(SpellRange.Medium, SpellRole.Attack, hitsAbove: true) },
        { "Pong Shot",            new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Rip And Tear",         new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Skillshot Slash",      new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "The Jokah",            new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Trickshot Alley",      new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Trident Of Poseidon",  new Profile(SpellRange.Medium, SpellRole.Attack) },
        { "Wolf Of Wallstreet",   new Profile(SpellRange.Medium, SpellRole.Attack) },

        // --- Long range ---
        { "Bailout",              new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Beam Of Sparta",       new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Coin Toss",            new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Get Over Here",        new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Hell Wave Fist",       new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Jigoku Flash Step",    new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Quarter Report",       new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Reload Shot",          new Profile(SpellRange.Long,   SpellRole.Attack) },
        { "Sickle Of The Night",  new Profile(SpellRange.Long,   SpellRole.Attack) },

        // --- Enhance: buff the owner or their basic attack, so cast them with room to spare ---
        { "Armory Of Hephaestus", new Profile(SpellRange.Short,  SpellRole.Enhance) },
        { "Cash Out",             new Profile(SpellRange.Short,  SpellRole.Enhance) },
        { "Demon Trigger",        new Profile(SpellRange.Short,  SpellRole.Enhance) },
        { "Tele-Frag Prism",      new Profile(SpellRange.Short,  SpellRole.Enhance) },

        // --- Zone: placed now, pays off when the opponent walks into it ---
        { "Gift Of Prometheus",   new Profile(SpellRange.Short,  SpellRole.Zone) },
        { "Loaded Dice",          new Profile(SpellRange.Short,  SpellRole.Zone) },
        { "Trap Card Trick",      new Profile(SpellRange.Short,  SpellRole.Zone) },
        { "Sun Of Apollo",        new Profile(SpellRange.Medium, SpellRole.Zone) },

        // --- Utility: defensive or positional, wanted in reaction to something ---
        { "Helm Of Hades",        new Profile(SpellRange.Short,  SpellRole.Utility) },
        { "Hourglass Of Chronos", new Profile(SpellRange.Short,  SpellRole.Utility) },
        { "Shot Reflector",       new Profile(SpellRange.Short,  SpellRole.Utility) },
    };

    public static Profile For(SpellData spell)
    {
        if (spell == null || string.IsNullOrEmpty(spell.spellName))
        {
            return Fallback;
        }

        return Profiles.TryGetValue(spell.spellName, out Profile profile) ? profile : Fallback;
    }

    /// <summary>
    /// The centre of a range band, in the same pixel units the stage AABBs and hurtboxes use.
    /// </summary>
    public static float IdealDistance(SpellRange range)
    {
        switch (range)
        {
            case SpellRange.Short: return 45f;
            case SpellRange.Long: return 210f;
            default: return 115f;
        }
    }

    /// <summary>How far either side of the ideal a spell still reads as the right call.</summary>
    public static float BandTolerance(SpellRange range)
    {
        switch (range)
        {
            case SpellRange.Short: return 45f;
            case SpellRange.Long: return 120f;
            default: return 75f;
        }
    }
}

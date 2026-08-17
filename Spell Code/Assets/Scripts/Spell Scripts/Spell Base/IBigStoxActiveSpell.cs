public interface IBigStoxActiveSpell
{
    bool DoesCrit { get; set; }
    bool AlwaysCrit { get; set; }
}

public static class BigStoxActiveSpellExtensions
{
    public static void ResolveCrit(this IBigStoxActiveSpell spell, PlayerController owner)
    {
        // Preserve the random call even for forced crits so the deterministic RNG
        // sequence remains the same as it is for a normal BigStox cast.
        int roll = GameManager.Instance.GetNextRandom(0, 100);
        spell.DoesCrit = spell.AlwaysCrit || roll < owner.stockStabilityModified;
    }

    public static void EnableForcedCrit(this IBigStoxActiveSpell spell)
    {
        spell.AlwaysCrit = true;
        spell.DoesCrit = true;
    }
}

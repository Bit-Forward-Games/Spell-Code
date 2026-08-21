using System;
using UnityEngine;

public class TouchOfMidas : SpellData
{
    [SerializeField] private Texture2D midasPalette;
    private short markedPID = -1;
    private ushort midasEffectCounter = 0;
    private const ushort baseMidasEffectTime = 120;
    public TouchOfMidas()
    {
        spellName = "Touch Of Midas";
        brands = new Brand[]{ Brand.DarkWeb, Brand.BigStox, Brand.Killeez };
        cooldown = 540;
        spellInput = 0b_0000_0000_0000_0101_0101_0010_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnHit, ProcCondition.OnUpdate, ProcCondition.OnCrit};
        projectilePrefabs = new GameObject[1];
        description = "Hitting this Spellcode turns the opponent into gold, causing all your attacks to Crit<sprite name=\"StockStability\"> against this opponent. The duration of this effect is determined by your Reps<sprite name=\"Reps\">. All Crits<sprite name=\"StockStability\"> grant 1 Rep<sprite name=\"Reps\">.";
        spawnOffsetX = 32;
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        markedPID = -1;
        // Clear the duration with the mark. Both are serialized, so leaving a stale value here
        // would put two machines' savestates out of step after a reset even though nothing is
        // marked on either. StockStability calls LoadSpell on every BigStox-branded spell, and
        // Touch Of Midas now matches that check, so this runs mid-match.
        midasEffectCounter = 0;
    }
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                markedPID = defender.pID;
                midasEffectCounter = (ushort)(baseMidasEffectTime + (owner.reps*10));
                // The marked player can be gone entirely (disconnect in a 3/4P match ->
                // GetPlayerByPID returns null), and an unguarded deref throws inside the resim,
                // which hard-freezes the sim rather than desyncing it.
                PlayerController markedTarget = GameManager.Instance.GetPlayerByPID(markedPID);
                if (markedTarget != null)
                {
                    markedTarget.InitializePalette(midasPalette);
                }


                if(GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified && !defender.hitboxData.ignoreEffectDamage)
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnCrit, defender);
                }
                break;
            case ProcCondition.OnHit:
                if(markedPID == defender.pID)
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnCrit, defender);
                }
                break;
            case ProcCondition.OnUpdate:
                // With nothing marked there is no timer to run down. The old code fell through to
                // the decrement below, so an idle counter underflowed 0 -> 65535 (ushort) every
                // match and then counted back down. It is re-seeded on each mark so nothing visibly
                // broke, but the value is part of the savestate, so it was churning the hash for no
                // reason and would have misread the moment anything else looked at it.
                if(markedPID < 0)
                {
                    break;
                }

                if(midasEffectCounter <= 0)
                {
                    // Same guard as above: clear the mark even when the player it pointed at is
                    // gone, otherwise markedPID stays set and every later hit keeps critting.
                    PlayerController markedOpponent = GameManager.Instance.GetPlayerByPID(markedPID);
                    if (markedOpponent != null)
                    {
                        markedOpponent.InitializePalette(markedOpponent.pID == 0? markedOpponent.npcPalette : markedOpponent.matchPalettes[markedOpponent.pID - 1]);
                    }
                    markedPID = -1;
                }
                else
                {
                    midasEffectCounter--;
                }
                break;
            case ProcCondition.OnCrit:
                //only grant resource on the first hit of a multihit per player
                if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile)|| defender.hitboxData.parentProjectile.ownerSpell == this)
                {
                    break;
                }

                owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnRepGain, defender);
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(markedPID);
        bw.Write(midasEffectCounter);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        markedPID = br.ReadInt16();
        midasEffectCounter = br.ReadUInt16();
    }
}

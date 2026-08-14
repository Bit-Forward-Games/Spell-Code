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
        spellInput = 0b_0000_0000_0000_0101_0101_0110_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnHit, ProcCondition.OnUpdate, ProcCondition.OnCrit};
        projectilePrefabs = new GameObject[1];
        description = "Hitting this Spellcode turns the opponent into gold, causing all your attacks to Crit<sprite name=\"StockStability\"> against this opponent. The duration of this effect is determined by your Reps<sprite name=\"Reps\">. All Crits<sprite name=\"StockStability\"> grant 1 Rep<sprite name=\"Reps\">";
        spawnOffsetX = 32;
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        markedPID = -1;
    }
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                markedPID = defender.pID;
                midasEffectCounter = (ushort)(baseMidasEffectTime + (owner.reps*10));
                GameManager.Instance.GetPlayerByPID(markedPID).InitializePalette(midasPalette);


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
                if(markedPID >=0 && midasEffectCounter <= 0)
                {
                    PlayerController markedOpponent = GameManager.Instance.GetPlayerByPID(markedPID);
                    markedOpponent.InitializePalette(markedOpponent.pID == 0? markedOpponent.npcPalette : markedOpponent.matchPalettes[markedOpponent.pID - 1]);
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

                //grant the resource
                owner.reps++;
                owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                break;
            default:
                break;
        }
    }
}

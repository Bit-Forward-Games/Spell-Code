using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class RodOfAsclepius : SpellData
{
    public int nextRepProc = 3;

    public RodOfAsclepius()
    {
        spellName = "Rod Of Asclepius";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[2] { ProcCondition.OnHitSpell, ProcCondition.OnDeath };
        brands = new Brand[1] { Brand.Killeez };
        description = "Heal 15 hp after every 3rd Rep<sprite name=\"Reps\"> gained.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        nextRepProc = 3;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnHitSpell:
                if(owner.reps >= nextRepProc)
                {
                    nextRepProc += 3;
                    owner.currentPlayerHealth = (ushort)Mathf.Min(owner.currentPlayerHealth + 15, owner.GetMaxHealth());
                    owner.SpawnToast("+15 HP", Color.green);
                }
                break;
            case ProcCondition.OnDeath:
                nextRepProc = 3;
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(nextRepProc);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        nextRepProc = br.ReadInt32();
    }
}

using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CrossmapClip : SpellData
{
    public static int flowStateIncrease = 180;
    public static int rangeThreshold = 200;
    public CrossmapClip()
    {
        spellName = "Crossmap Clip";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] {ProcCondition.OnHit };
        brands = new Brand[1] { Brand.VWave };
        description = $"Dealing Damage from far away grants {flowStateIncrease/60} seconds of Flow State<sprite name=\"FlowState\">.";

    }

    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnHit:

                if (cooldownCounter <= 0 && IsFarEnough(defender))
                {
                    owner.SpawnToast($"+{flowStateIncrease/60}SEC FLOW STATE", GameManager.colors["green"]);
                    owner.flowState = (ushort)Mathf.Min(owner.flowState + flowStateIncrease,FlowState.maxFlowState);
                    cooldownCounter = cooldown;
                }
                break;
            default:
                break;
        }
    }

    public bool IsFarEnough(PlayerController defender)
    {
        if (owner == null || defender == null) return false;

        
        // Compute squared distance (avoid square root):
        Fixed dx = Fixed.Abs(owner.position.X - defender.position.X) / Fixed.FromInt(100);
        Fixed dy = Fixed.Abs(owner.position.Y - defender.position.Y) / Fixed.FromInt(100);
        Fixed distSq = (dx * dx) + (dy * dy);
        Fixed squaredThreshold = Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100) * Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100);

        return distSq > squaredThreshold;
    }
}

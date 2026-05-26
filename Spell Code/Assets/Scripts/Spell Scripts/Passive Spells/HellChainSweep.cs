using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HellChainSweep : SpellData
{
    public HellChainSweep()
    {
        spellName = "Hell-Chain Sweep";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnSlide};
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        description = "Deal damage with your slide.\nWhen above 50% Demon Aura<sprite name=\"DemonAura\">, this spell breaks armor.";
        spawnOffsetX = 15;
        spawnOffsetY = 0;
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
            case ProcCondition.OnSlide:
            if(cooldownCounter <= 0)
                {
                    
                    cooldownCounter = cooldown;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[owner.demonAura >= 50?1:0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(16), Fixed.FromInt(0)));
                }
                break;
            default:
                break;
        }
    }

}

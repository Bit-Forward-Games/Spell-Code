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
        procConditions = new ProcCondition[] { ProcCondition.OnSlide, ProcCondition.ActiveOnHit};
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        description = "Your slide attacks.\nHit This: +20% Demon Aura<sprite name=\"DemonAura\">\n When above 50% Demon Aura<sprite name=\"DemonAura\">, this spell is empowered.";
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
            case ProcCondition.ActiveOnHit:
                owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                owner.SpawnToast("+20 DEMON AURA", Color.red);
                break;
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

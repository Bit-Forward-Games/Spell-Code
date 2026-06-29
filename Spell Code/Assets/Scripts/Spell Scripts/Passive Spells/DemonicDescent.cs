using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class DemonicDescent : SpellData
{

    public DemonicDescent()
    {
        spellName = "Demonic Descent";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate};
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[1];
        description = "While at 100% Demon Aura <sprite name=\"DemonAura\">, you gain mobility, and you gain a damaging aura.\n";
        spawnOffsetX = 0;
        spawnOffsetY = 36;
    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.OnUpdate:
                
                if(owner.demonAura>= 100)
                {
                    owner.runSpeed = Fixed.FromInt((owner.charData.runSpeed + 15)/10);
                    owner.jumpForce = Fixed.FromInt(owner.charData.jumpForce + 2);
                    owner.slideSpeed = Fixed.FromInt((owner.charData.slideSpeed + 20)/10);

                    if (!projectileInstances[0].activeSelf)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX ), Fixed.FromInt(spawnOffsetY)));
                    }
                }
                else
                {
                    owner.runSpeed = Fixed.FromInt(owner.charData.runSpeed/10);
                    owner.jumpForce = Fixed.FromInt(owner.charData.jumpForce);
                    owner.slideSpeed = Fixed.FromInt(owner.charData.slideSpeed/10);
                }
                break;
            default:
                break;
        }
    }
}

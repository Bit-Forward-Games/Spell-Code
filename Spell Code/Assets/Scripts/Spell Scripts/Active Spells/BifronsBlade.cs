using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BifronsBlade : SpellData
{
    public BifronsBlade()
    {
        spellName = "BifronsBlade";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_1100_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[3] { ProcCondition.ActiveOnHit, ProcCondition.OnCastBasic, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[2];
        description = "Slash upwards with a two-faced blade, granting \"Demon Aura\" on hit. your basic attack then slashes downward, dealing damage based on your \"Demon Aura\".";

    }

   
  
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.ActiveOnHit:
                if (defender.hitboxData.basicAttackHitbox) //if it is the basic attack slash, which is a basic attack hitbox, deal effect damage based on the amount of Demon Aura consumed, then consume all Demon Aura. Otherwise, gain 20 Demon Aura.
                {
                    defender.TakeEffectDamage(owner.demonAura/4, owner);
                }
                else //if it is the spell hitbox, gain 20 Demon Aura, but only if the player is not already at max Demon Aura
                {
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                }
                break;
            case ProcCondition.OnCastBasic:
                
                if (owner.basicSpawnOverride)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
            break;
                case ProcCondition.ActiveOnCast:
                    owner.basicSpawnOverride = true; // Set the flag to override the basic attack spawn
                break;
            default:
                break;
        }
    }
}

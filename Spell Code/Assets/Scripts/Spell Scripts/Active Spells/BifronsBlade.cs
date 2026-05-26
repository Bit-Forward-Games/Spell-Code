using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class BifronsBlade : SpellData
{
    public BifronsBlade()
    {
        spellName = "Bifrons Blade";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_1100_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnCastBasic, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[2];
        description = "Medium-range slash.\nEnhance next basic attack to break armor and consume all Demon Aura<sprite name=\"DemonAura\"> for extra damage.";
        spawnOffsetX = 25;
        spawnOffsetY = 40;

    }




    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                if (defender.hitboxData.basicAttackHitbox) //if it is the basic attack slash,Consume all Demon Aura on hitting an enemy with a basic attack, dealing that much bonus damage.
                {
                    if (owner.demonAura > 0)
                    {
                        int demonDiv = owner.demonAura / 10;
                        int damageToDeal = demonDiv * demonDiv / 2;
                        defender.TakeEffectDamage(damageToDeal, owner, GameManager.colors["red"]);
                        owner.demonAura = 0;
                    }
                }
                break;
            case ProcCondition.OnCastBasic:
                
                if (owner.basicSpawnOverride == spellName)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
            break;
                case ProcCondition.ActiveOnCast:
                    owner.basicSpawnOverride = spellName; // Set the flag to override the basic attack spawn
                break;
            default:
                break;
        }
    }
}

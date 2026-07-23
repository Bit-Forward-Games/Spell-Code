using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HellWaveFist : SpellData
{
    public HellWaveFist()
    {
        spellName = "Hell Wave Fist";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0100_0000_0010;
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {};
        projectilePrefabs = new GameObject[2];

        description = "Long-range energy blast.\nWhen Demon Aura<sprite name=\"DemonAura\"> is B rank or higher, This Spellcode is enhanced.";

    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;



        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {
            // Reset the activate flag
            activateFlag = false;

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null)
            {
                
                if(owner.demonAura >= 40)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                else
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
            }
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
        }


    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            //Spell effects take place in the update function
            
            default:
                break;
        }
    }
}

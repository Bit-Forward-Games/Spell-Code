using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AsuranBlades : SpellData
{
    public AsuranBlades()
    {
        spellName = "Asuran Blades";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 120;
        spellInput = 0b_0000_0000_0000_0000_0000_0010_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnSlide };
        projectilePrefabs = new GameObject[4];

        description = "Throw 3 shurikens downward.\nIf 50%+ Demon Aura<sprite name=\"DemonAura\">, throw more shurikens.";

        spawnOffsetX = 15;
        spawnOffsetY = 0;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        //--------------OLD STAGGERED LAUNCH------------
        // if (projectileInstances[0].activeSelf && projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == 3)
        // {
        //     ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        // }

        // if (projectileInstances[1].activeSelf && projectileInstances[1].GetComponent<BaseProjectile>().logicFrame == 3)
        // {
        //     ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY + 2)));
        // }


        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {
            // Reset the activate flag
            activateFlag = false;
            owner.vSpd = Fixed.FromInt(8); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(-2) : Fixed.FromInt(2); // Propel the player backwatds slightly

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 2)
            {
                
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-2)));
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY + 2)));
                if(owner.demonAura >= 50)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX+1), Fixed.FromInt(spawnOffsetY+2)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX+3), Fixed.FromInt(spawnOffsetY+2)));
                
                }
            }
            cooldownCounter = vibeCasted?cooldown+60:cooldown;
            if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            vibeCasted = false;
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

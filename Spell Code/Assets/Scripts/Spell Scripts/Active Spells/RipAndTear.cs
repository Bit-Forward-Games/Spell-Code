using UnityEngine;
using System.Linq;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class RipAndTear : SpellData
{
    public RipAndTear()
    {
        spellName = "Rip And Tear";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_1110_0001_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {};
        projectilePrefabs = new GameObject[1];
        description = "Encircling blade wheel.\nTravel along surfaces, traveling farther based on Demon Aura<sprite name=\"Demon Aura\">.";
        spawnOffsetX = 0;

    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;

        //if the projectile is active, rip along the ground
        if (projectileInstances[0].activeSelf)
        {
            if (owner.isGrounded)
            {
                owner.hSpd = owner.facingRight ? Fixed.FromInt(6) : Fixed.FromInt(-6); // Propel the player forward
            }
            if (owner.touchingLeftWall)
            {
                owner.vSpd = owner.facingRight ? Fixed.FromInt(-6) : Fixed.FromInt(6); // Propel the player forward
            }
            else if (owner.touchingRightWall)
            {
                owner.vSpd = owner.facingRight ? Fixed.FromInt(6) : Fixed.FromInt(-6); // Propel the player forward
            }
        }


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
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = vibeCasted?cooldown+60:cooldown;
            if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            vibeCasted = false;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            //all spell effects are done in the projectiles' logic
            default:
                break;
        }

    }
}

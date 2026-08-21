using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;

public class DemonTrigger : SpellData
{
    public DemonTrigger()
    {
        spellName = "Demon Trigger";
        brands = new Brand[] { Brand.DarkWeb, Brand.VWave, Brand.DemonX };
        cooldown = 600;
        spellInput = 0b_0000_0000_0000_0110_0011_0110_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.OnUpdate, ProcCondition.OnCastBasic, ProcCondition.OnSlide, ProcCondition.OnJump};
        description = "Embody that which never cries until you take damage, enhancing your basic attack, air jump, and slide.";
        projectilePrefabs = new GameObject[6];// 0 - aura, 1 - blade, 2 - gun, 3 - green bullet, 4 - red bullet, 5 - jump
        spawnOffsetX = 0;
        spawnOffsetY = 0;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //all of the special effect logic for this spellcode lies in the projectile script
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (projectileInstances[0].activeSelf)
                {
                    SetBasicEnhancement();
                }
                //bullet spawning logic
                if (projectileInstances[2].activeSelf)
                {
                    if(owner.state != PlayerState.Slide)
                    {
                        ProjectileManager.Instance.DeleteProjectile(projectileInstances[2].GetComponent<BaseProjectile>());
                    }
                    
                    //green bullet spawn
                    if (projectileInstances[2].GetComponent<BaseProjectile>().logicFrame ==
                        projectileInstances[2].GetComponent<BaseProjectile>().animFrames.frameLengths.Take(4).Sum() + 1)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX + 32), Fixed.FromInt(spawnOffsetY + 12)));   
                    }
                    //red bullet spawn
                    if (projectileInstances[2].GetComponent<BaseProjectile>().logicFrame ==
                        projectileInstances[2].GetComponent<BaseProjectile>().animFrames.frameLengths.Take(6).Sum() + 1)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX + 16), Fixed.FromInt(spawnOffsetY + 12)));   
                    }
                }
                //sword lunge logic
                if (projectileInstances[1].activeSelf && 
                projectileInstances[1].GetComponent<BaseProjectile>().logicFrame ==
                projectileInstances[1].GetComponent<BaseProjectile>().frameData.startFrames[0] - 1)
                {
                    owner.vSpd = Fixed.FromInt(-4); // Launch the player downward slightly
                    owner.hSpd = owner.facingRight ? Fixed.FromInt(6) : Fixed.FromInt(-6); // Propel the player forward
                }
                break;
            case ProcCondition.OnCastBasic:
                if (projectileInstances[0].activeSelf)
                {
                    if (owner.basicSpawnOverride == spellName && basicEnhanceActive)
                    {
                        basicEnhanceActive = false;
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    }
                }
                break;
            case ProcCondition.OnSlide:
                if (projectileInstances[0].activeSelf)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                break;
            case ProcCondition.OnJump:
                if (projectileInstances[0].activeSelf && owner.jumpCount < owner.maxJumpCount - 1)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[5].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                break;
            default:
                break;
        }
    }
}

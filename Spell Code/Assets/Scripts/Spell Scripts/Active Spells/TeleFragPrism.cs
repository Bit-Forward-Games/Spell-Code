using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;

public class TeleFragPrism : SpellData
{
    public TeleFragPrism()
    {
        spellName = "Tele-Frag Prism";
        cooldown = 300;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_0101_1010_0000_0100;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate, ProcCondition.OnCastBasic, ProcCondition.ActiveOnCast};
        brands = new Brand[1] { Brand.VWave };
        projectilePrefabs = new GameObject[3];
        spawnOffsetX = 0;
        spawnOffsetY = 0;
        description = "Place down a refracting prism.\nYour next Basic Attack teleports you across the prism, dealing damage at your new location.";
    }


    
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnCastBasic:
                if(projectileInstances[0].activeSelf)
                {
                    owner.TeleportToDestination(projectileInstances[1].GetComponent<BaseProjectile>().position);
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                    //spawn the explosion
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                }
                
                break;
            case ProcCondition.ActiveOnCast:
                owner.basicSpawnOverride = spellName;
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                break;
            case ProcCondition.OnUpdate:
                //if you overwrite your basic replacement, delete te prism and reticle
                if(owner.basicSpawnOverride != spellName)
                {
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[0].GetComponent<BaseProjectile>());
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                    break;
                }
                //handle the prism vfx

                //just making sure the reticle exists if the prism does
                if (projectileInstances[0].activeSelf && !projectileInstances[1].activeSelf)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                }
                else if(!projectileInstances[0].activeSelf && projectileInstances[1].activeSelf)
                {
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                }

                //handle the reticle's position
                if (projectileInstances[1].activeSelf)
                {
                    BaseProjectile prismProjectile = projectileInstances[0].GetComponent<BaseProjectile>();
                    BaseProjectile reflectedProjectile = projectileInstances[1].GetComponent<BaseProjectile>();
                    FixedVec2 prismPosition = prismProjectile.position;
                    FixedVec2 ownerPosition = owner.position;

                    reflectedProjectile.position = new FixedVec2(
                        prismPosition.X - (ownerPosition.X - prismPosition.X),
                        prismPosition.Y - (ownerPosition.Y - prismPosition.Y)
                    );
                }
                break;
            default:
                break;
        }
    }

    
}

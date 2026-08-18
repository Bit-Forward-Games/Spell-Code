using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;

public class HourglassOfChronos : SpellData
{
    public ushort savedHealth = 255;
    public HourglassOfChronos()
    {
        spellName = "Hourglass Of Chronos";
        cooldown = 480;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_1000_0111_0000_0100;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast};
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[2];
        spawnOffsetX = 0;
        spawnOffsetY = 0;
        description = "Place down an hourglass. Upon re-casting, or after the hourglass runs out, go back to your previous position and health, dealing damage at your new location when you arrive.";
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 2) return;
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if(projectileInstances[0].activeSelf && 
        projectileInstances[0].GetComponent<BaseProjectile>().logicFrame == projectileInstances[0].GetComponent<BaseProjectile>().animFrames.frameLengths.Sum()-1)
        {
            HourglassProc();
        }
    }

    public void HourglassProc(int cooldownRefund = 0)
    {
        
        owner.TeleportToDestination(projectileInstances[0].GetComponent<BaseProjectile>().position);
        ProjectileManager.Instance.DeleteProjectile(projectileInstances[0].GetComponent<BaseProjectile>());
        //spawn the explosion
        ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
        if(savedHealth != 255) owner.currentPlayerHealth = savedHealth;
        savedHealth = 255;

        int baseCooldown = owner.vibeCoding?(int)(cooldown*1.25f):cooldown;
        if (cooldownRefund != 0)
        {
            cooldownCounter = Mathf.Max(baseCooldown-cooldownRefund + (owner.vibeCoding?60:0),0);
        }
        else
        {
            cooldownCounter = baseCooldown;
            
        }
        
    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                if (cooldownCounter > 0)break;

                if(projectileInstances[0].activeSelf)
                {   
                    int cooldownRefund =  
                    projectileInstances[0].GetComponent<BaseProjectile>().animFrames.frameLengths.Sum() - 
                    projectileInstances[0].GetComponent<BaseProjectile>().logicFrame;
                    HourglassProc(cooldownRefund);
                }
                else
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                    savedHealth = owner.currentPlayerHealth;
                }

                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(savedHealth);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        savedHealth = br.ReadUInt16();
    }
    
}

using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class JigokuFlashStep : SpellData
{
    short markedOpponentPID = -1;
    const int teleportOffset = 20;
    public JigokuFlashStep()
    {
        spellName = "Jigoku Flash Step";
        cooldown = 360;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_0110_0001_0000_0100;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnCast };
        brands = new Brand[1] { Brand.DemonX };
        projectilePrefabs = new GameObject[2];
        description = "Longe-range kunai.\n On-hit, mark the opponent. Your next cast teleports you behind the marked opponent.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        markedOpponentPID = -1;
    }

    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 2) return;


        //handle the marked vfx
        if (projectileInstances[1].activeSelf)
        {
            // The marked player can vanish mid-mark (disconnect in a 3/4P match). GetPlayerByPID would
            // then return null (or an out-of-range pID would throw) and the deref crashes the sim on
            // every client. Drop the mark instead of dereferencing a missing player
            PlayerController marked = markedOpponentPID >= 0 ? GameManager.Instance.GetPlayerByPID(markedOpponentPID) : null;
            if (marked != null)
            {
                projectileInstances[1].GetComponent<BaseProjectile>().position = marked.position;
            }
            else
            {
                ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                markedOpponentPID = -1;
            }
        }
        else
        {
            markedOpponentPID = -1;
        }

        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

        if (activateFlag)
        {
            // Reset the activate flag
            activateFlag = false;
            owner.vSpd = Fixed.FromInt(3); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(-3) : Fixed.FromInt(3); // Propel the player backwatds slightly

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            
            cooldownCounter = owner.vibeCoding?(int)(cooldown*1.25f):cooldown;
        }
        

        
        

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                markedOpponentPID = defender.pID;
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), true, GameManager.Instance.GetPlayerByPID(markedOpponentPID).position);
                break;
            case ProcCondition.OnCast:
                if(markedOpponentPID >= 0)
                {
                    // Skip the teleport if the marked player disconnected (null), but still clear the
                    // mark + projectile so state stays consistent on every client.
                    PlayerController markedOpponent = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                    if (markedOpponent != null)
                    {
                        owner.position = markedOpponent.position + new FixedVec2(Fixed.FromInt(markedOpponent.facingRight?-teleportOffset:teleportOffset), Fixed.FromInt(0));
                        owner.facingRight = markedOpponent.facingRight;
                    }
                    markedOpponentPID = -1;
                    ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());

                }
                
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(markedOpponentPID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        markedOpponentPID = br.ReadInt16();
    }
    
}

using System;
using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SickleOfTheNight : SpellData
{
    [NonSerialized] public short targetPID = -1;
    public SickleOfTheNight()
    {
        spellName = "Sickle Of The Night";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0001_1110_0000_0100; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit, ProcCondition.OnCastBasic, ProcCondition.ActiveOnCast, ProcCondition.OnUpdate };
        projectilePrefabs = new GameObject[5];
        description = "Long-range Crescent slash.\n On hit, enhance next basic attack home in on the marked opponent.\n Spawns more enhanced basic attacks when in Flow State<sprite name=\"FlowState\">.";
        spawnOffsetY = 32;

    }




    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                owner.vSpd = Fixed.FromInt(3);
                break;
            case ProcCondition.ActiveOnHit:
                if (!defender.hitboxData.basicAttackHitbox)
                {
                    owner.basicSpawnOverride = spellName; // Set the flag to override the basic attack spawn
                    targetPID = defender.pID;
                    PlayerController cachedPlayer = GameManager.Instance.GetPlayerByPID(targetPID);
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), cachedPlayer.facingRight, cachedPlayer.position, true);

                }
                break;
            case ProcCondition.OnCastBasic:
                
                if (owner.basicSpawnOverride == spellName)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    if(owner.flowState > 0)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX-64), Fixed.FromInt(spawnOffsetY + 64)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX-64), Fixed.FromInt(spawnOffsetY - 64)));
                    }
                }
            break;
            case ProcCondition.OnUpdate:
                if (projectileInstances[4].activeSelf)
                {
                    // Guard EVERY deref, the marked player can be gone entirely (disconnect in a 3/4P
                    // match -> GetPlayerByPID returns null; stale -1 pID would even index players[-2])
                    // an unguarded deref crashes the sim on every client.
                    PlayerController marked = targetPID >= 0 ? GameManager.Instance.GetPlayerByPID(targetPID) : null;
                    if (marked != null && marked.isAlive)
                    {
                        projectileInstances[4].GetComponent<BaseProjectile>().position = marked.position;
                        projectileInstances[4].GetComponent<BaseProjectile>().facingRight = marked.facingRight;
                    }
                    else
                    {
                        targetPID = -1;
                    }
                }
                if(owner.basicSpawnOverride != spellName)
                {
                    targetPID = -1;
                }
                break;
            default:
                break;
        }
    }
    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(targetPID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        targetPID = br.ReadInt16();
    }
}

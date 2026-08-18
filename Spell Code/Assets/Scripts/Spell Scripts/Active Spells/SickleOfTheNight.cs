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
        description = "Long-range Crescent slash. On hit, enhance next basic attack to hone in on the marked opponent. Spawns more enhanced basic attacks when in Flow State<sprite name=\"FlowState\">.";
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
                    int extraSpellIndex = owner.extraSpells != null ? owner.extraSpells.IndexOf(this) : -1;
                    owner.basicSpawnOverrideVariant = extraSpellIndex >= 0 && extraSpellIndex < byte.MaxValue
                        ? (byte)(extraSpellIndex + 1)
                        : (byte)0;
                    targetPID = defender.pID;
                    // `defender` is the authoritative object that was just hit. Resolving it again by
                    // PID can legitimately return null if a sparse online slot disconnects during the
                    // hit, so use the known object for the initial world-space mark position. The mark
                    // projectile validates the live roster and fades itself on its next update.
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), defender.facingRight, defender.position, true);

                }
                break;
            case ProcCondition.OnCastBasic:
                
                if (OwnsPendingBasicOverride())
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
                if(!OwnsPendingBasicOverride())
                {
                    targetPID = -1;
                }
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Identifies which Sickle instance armed the shared basic override. Variant zero is the normal
    /// inventory spell; Jokah copies use their stable extraSpells index plus one. The variant is
    /// already serialized and hashed with PlayerController, so rollback restores this ownership.
    /// </summary>
    public bool OwnsPendingBasicOverride()
    {
        if (owner == null || owner.basicSpawnOverride != spellName)
        {
            return false;
        }

        int extraSpellIndex = owner.extraSpells != null ? owner.extraSpells.IndexOf(this) : -1;
        if (extraSpellIndex >= 0)
        {
            return extraSpellIndex < byte.MaxValue
                && owner.basicSpawnOverrideVariant == (byte)(extraSpellIndex + 1);
        }

        return owner.spellList != null
            && owner.spellList.Contains(this)
            && owner.basicSpawnOverrideVariant == 0;
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

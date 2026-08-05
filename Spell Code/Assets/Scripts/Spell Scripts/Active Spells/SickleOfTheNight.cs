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
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnCastBasic, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[4];
        description = "Long-range Crescent slash.\n On hit, enhance next basic attack to break armor and consume all Demon Aura<sprite name=\"DemonAura\"> for extra damage.";
        spawnOffsetX = 25;
        spawnOffsetY = 40;

    }




    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                targetPID = defender.pID;
                owner.basicSpawnOverride = spellName; // Set the flag to override the basic attack spawn
                break;
            case ProcCondition.OnCastBasic:
                
                if (owner.basicSpawnOverride == spellName)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    if(owner.flowState > 0)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX-16), Fixed.FromInt(spawnOffsetY + 16)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX-16), Fixed.FromInt(spawnOffsetY - 16)));
                    }
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

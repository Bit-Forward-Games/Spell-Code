using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class MineCrafter : SpellData
{
    private byte projectileSpawnIndex = 0;
    private const byte maxProjectiles = 3;

    public MineCrafter()
    {
        spellName = "Mine Crafter";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] { ProcCondition.OnSlide };
        brands = new Brand[1] { Brand.VWave };
        description = "While in Flow State<sprite name=\"FlowState\">, slide crafts a mine.";

        projectilePrefabs = new GameObject[maxProjectiles];
        spawnOffsetX = 0;
        spawnOffsetY = 10;
    }

    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        //OnCast proc: Check if in flow state, if so, spawn an Overclock Explosion
        switch(targetProcCon)
        {
            case ProcCondition.OnSlide:
                
                if (owner.flowState > 0)
                {
                    if(cooldownCounter <= 0)
                    {
                        
                        cooldownCounter = cooldown;
                        

                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileSpawnIndex].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                        projectileSpawnIndex = (byte)((projectileSpawnIndex+1) % maxProjectiles);
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
        bw.Write(projectileSpawnIndex);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        projectileSpawnIndex = br.ReadByte();
    }
}

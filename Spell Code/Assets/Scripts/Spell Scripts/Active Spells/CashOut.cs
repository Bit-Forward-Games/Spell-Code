using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CashOut : SpellData
{
    public bool doesCrit = false;
    public CashOut()
    {
        spellName = "Cash Out";
        brands = new Brand[]{ Brand.BigStox };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0000_0001_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[1] { ProcCondition.ActiveOnCast};
        projectilePrefabs = new GameObject[8];

        description = "Short-ranged burst shot.\nRandom chance based on Stock Stability<sprite name=\"StockStability\"> to enhance number and damage.\nGain 10% Stock Stability<sprite name=\"StockStability\">.";

        spawnOffsetX = 15;
        //spawnOffsetY = 0;
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
        if (owner != null && !owner.suppressSpellLoadSideEffects)
        {
            owner.stockStability += 10;
            owner.SpawnToast("+10% STOCK STABILITY", Color.blue);
        }
        doesCrit = false;
    }
    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;



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
            owner.hSpd = owner.facingRight ? Fixed.FromInt(-4) : Fixed.FromInt(4); // Propel the player backwatds slightly

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null)
            {

                if (doesCrit)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-2)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-1)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY+1)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY + 2)));
                }
                else
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[5].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-1)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[6].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[7].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY+1)));
                }
                
            }
            cooldownCounter = cooldown;
        }


    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                int roll = GameManager.Instance.GetNextRandom(0, 100);
                Debug.Log($"[COINTOSS SYNC] Frame={GameManager.Instance.frameNumber} roll={roll} randomCallCount={GameManager.Instance.randomCallCount}");
                doesCrit = roll < owner.stockStability;
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doesCrit);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
    }
}

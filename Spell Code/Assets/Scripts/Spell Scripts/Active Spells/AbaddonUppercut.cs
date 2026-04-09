using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AbaddonUppercut : SpellData
{
    public bool doubleHitReady;
    public AbaddonUppercut()
    {
        spellName = "Abaddon Uppercut";
        brands = new Brand[]{ Brand.DemonX };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0001_0001_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[2] { ProcCondition.ActiveOnHit, ProcCondition.ActiveOnCast };
        projectilePrefabs = new GameObject[1];
        description = "Short-range rising Uppercut.\nHit this: +20% Demon Aura<sprite name=\"DemonAura\">\n and double-hits if over 50% Demon Aura<sprite name=\"DemonAura\">.\n This spell is armored.";

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
            owner.vSpd = Fixed.FromInt(15); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(2) : Fixed.FromInt(-2); // Propel the player forward

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 0)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }
            cooldownCounter = cooldown;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            //ActiveOnHit: Gain 10 Demon Aura on hitting an enemy with this spell.
            case ProcCondition.ActiveOnHit:
                owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                owner.SpawnToast("+20 DEMON AURA", Color.red);
                if(owner.demonAura > 50 && doubleHitReady)
                {
                    //effectively do the uppercut again
                    owner.vSpd = Fixed.FromInt(10); // Launch the player upwards slightly
                    owner.hSpd = owner.facingRight ? Fixed.FromInt(2) : Fixed.FromInt(-2); // Propel the player forward

                    // Instantiate the projectile prefab at the player's position
                    // Assuming you have a reference to the player GameObject
                    if (owner != null && projectilePrefabs.Length > 0)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                    }
                    doubleHitReady = false;
                }
                break;
            case ProcCondition.ActiveOnCast:
                owner.hitstunOverride = true;
                doubleHitReady = true;
                break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doubleHitReady);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doubleHitReady = br.ReadBoolean();
    }
}

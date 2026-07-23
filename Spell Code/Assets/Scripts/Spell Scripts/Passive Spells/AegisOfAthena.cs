using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AegisOfAthena : SpellData
{
    //bool parryStored = false;
    public AegisOfAthena()
    {
        spellName = "Aegis Of Athena";
        cooldown = 120;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnParry, ProcCondition.OnCast, ProcCondition.OnHit, ProcCondition.OnCastEnd };
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[2];
        spawnOffsetX = 0;
        spawnOffsetY = 36;
        description = "Your parry reflects projectiles.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        //parryStored = false;
    }

    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        // if (!projectileInstances[0].activeSelf && parryStored)
        // {
        //     ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX ), Fixed.FromInt(spawnOffsetY)));
        // }
        // if (!parryStored)
        // {
        //     ProjectileManager.Instance.DeleteProjectile(projectileInstances[0].GetComponent<BaseProjectile>());
        // }

    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnParry:
                //parryStored = true;

                //reflect the projectile
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX ), Fixed.FromInt(spawnOffsetY)));
                BaseProjectile reflectedProjectile = owner.hitboxData.parentProjectile;
                reflectedProjectile.ownerBackup = reflectedProjectile.owner;
                reflectedProjectile.owner = owner;
                reflectedProjectile.ResetValues();;
                reflectedProjectile.hSpeed = -reflectedProjectile.hSpeed;
                reflectedProjectile.vSpeed = -reflectedProjectile.vSpeed;
                reflectedProjectile.playerHitArr = new bool[4] { false, false, false, false };

                cooldownCounter = owner.vibeCoding? (int)(cooldown*1.25f) : cooldown;
                break;
            default:
                break;
        }
    }
    // public override void Serialize(System.IO.BinaryWriter bw)
    // {
    //     base.Serialize(bw);
    //     bw.Write(parryStored);
    // }

    // public override void Deserialize(System.IO.BinaryReader br)
    // {
    //     base.Deserialize(br);
    //     parryStored = br.ReadBoolean();
    // }
    
}

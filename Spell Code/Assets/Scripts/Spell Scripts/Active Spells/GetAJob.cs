using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GetAJob : SpellData
{
    public bool doesCrit = false;
    public GetAJob()
    {
        spellName = "Get A Job";
        brands = new Brand[]{ Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0011_0100_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit };
        projectilePrefabs = new GameObject[2];
        description = "Medium-range lunging job application.\nThis spell has armor.\nGains super armor, extra range, and stun on \"Crit\"<sprite name=\"StockStability\">.";
        spawnOffsetX = 36;
        spawnOffsetY = 36;
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
        // if (owner != null && !owner.suppressSpellLoadSideEffects)
        // {
        //     owner.stockStability += 10;
        //     owner.SpawnToast("+10% STOCK STABILITY", GameManager.colors["blue"]);
        // }
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
            int speedBoost = doesCrit ? 12 : 8; // Example: If it's a critical hit, increase speed boost
            // Reset the activate flag
            activateFlag = false;
            owner.vSpd = Fixed.FromInt(4); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(speedBoost) : Fixed.FromInt(-speedBoost); // Propel the player forward

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[(doesCrit ? 1 : 0)].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                //if the spell will crit,...
                if (doesCrit)
                {
                    //Play the Critical Cast VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CRITICAL_CAST, new FixedVec2(owner.position.X + Fixed.FromInt(spawnOffsetX), owner.position.Y + Fixed.FromInt(spawnOffsetY)), owner.pID, owner.facingRight);

                    //Play the Critical Cast SFX
                    SFX_Manager.Instance.PlaySound(Sounds.CRITICAL_CAST);
                }
            }
            cooldownCounter = owner.vibeCoding?(int)(cooldown*1.25f):cooldown;
            //if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            //vibeCasted = false;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            
            case ProcCondition.ActiveOnCast:
                doesCrit = GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified;
                owner.superArmor = doesCrit;
                owner.armor = !doesCrit;
                break;
            case ProcCondition.ActiveOnHit:
                if (doesCrit)
                {
                    defender.TakeEffectDamage(StockStability.bigStoxCritDamage,owner, GameManager.colors["blue"]);
                }
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

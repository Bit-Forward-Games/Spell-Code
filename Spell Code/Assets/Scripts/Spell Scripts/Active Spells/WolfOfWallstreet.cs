using UnityEngine;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class WolfOfWallstreet : SpellData, IBigStoxActiveSpell
{
    public bool doesCrit = false;
    public const int demonAuraThreshold = 80;
    bool IBigStoxActiveSpell.DoesCrit { get => doesCrit; set => doesCrit = value; }
    bool IBigStoxActiveSpell.AlwaysCrit { get; set; }
    public WolfOfWallstreet()
    {
        spellName = "Wolf Of Wallstreet";
        brands = new Brand[]{ Brand.DarkWeb, Brand.DemonX, Brand.BigStox };
        cooldown = 360;
        spellInput = 0b_0000_0000_0000_0111_1001_0010_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit, ProcCondition.OnHitSpell};
        projectilePrefabs = new GameObject[2];
        description = "Medium-range lunging job application. This Spellcode has armor. Gains super armor, extra range, and stun on Crit<sprite name=\"StockStability\">.";
        //spawnOffsetX = 36;
        spawnOffsetY = 36;
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
        doesCrit = false;
    }
    public override void SpellUpdate()
    {
        if (projectileInstances.Count < 1) return;
        int speedBoost = doesCrit ? 10 : 8; // Example: If it's a critical hit, increase speed boost

        BaseProjectile baseProj = projectileInstances[0].GetComponent<BaseProjectile>();
        BaseProjectile critProj = projectileInstances[1].GetComponent<BaseProjectile>();
        if (baseProj.logicFrame == baseProj.frameData.startFrames[0] || 
            baseProj.logicFrame == baseProj.frameData.startFrames[1] ||
            critProj.logicFrame == critProj.frameData.startFrames[0] ||
            critProj.logicFrame == critProj.frameData.startFrames[1])
        {
            owner.vSpd = Fixed.FromInt(4); // Launch the player upwards slightly
            owner.hSpd = owner.facingRight ? Fixed.FromInt(speedBoost) : Fixed.FromInt(-speedBoost); // Propel the player forward
        }
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            return;
        }
        if (activateFlag)
        {
            
            // Reset the activate flag
            activateFlag = false;
            

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
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
            //if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            //vibeCasted = false;
        }

    }


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            
            case ProcCondition.ActiveOnCast:
                if(owner.demonAura >= demonAuraThreshold)
                {
                    this.EnableForcedCrit();
                }
                else
                {
                    this.DisableForcedCrit();
                }
                this.ResolveCrit(owner);
                owner.superArmor = doesCrit;
                owner.armor = !doesCrit;
                break;
            case ProcCondition.ActiveOnHit:
                if (doesCrit)
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnCrit, defender);
                }
                
                if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                {
                    break;
                }
                foreach(SpellData spell in defender.spellList)
                {
                    if (spell.cooldownCounter > 1)
                    {
                        
                        //stolenEnergy ++;
                        owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnRankUp, defender);
                        spell.cooldownCounter = spell.cooldown;
                    }
                }
                break;
            case ProcCondition.OnHitSpell:
                if(owner.demonAura >= demonAuraThreshold && this != defender.hitboxData.parentProjectile.ownerSpell)
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnCrit, defender);
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

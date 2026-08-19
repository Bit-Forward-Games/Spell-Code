using UnityEngine;
using BestoNet.Types;
using System.Collections.Generic;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CashOut : SpellData, IBigStoxActiveSpell
{
    public bool doesCrit = false;
    bool IBigStoxActiveSpell.DoesCrit { get => doesCrit; set => doesCrit = value; }
    bool IBigStoxActiveSpell.AlwaysCrit { get; set; }
    public CashOut()
    {
        spellName = "Cash Out";
        brands = new Brand[]{ Brand.BigStox };
        cooldown = 180;
        spellInput = 0b_0000_0000_0000_0000_0010_0001_0000_0011; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnCast, ProcCondition.OnCastBasic, ProcCondition.ActiveOnHit};
        projectilePrefabs = new GameObject[10];

        description = "Enhance basic attack into short-ranged burst shot. On Crit<sprite name=\"StockStability\">, The enhanced basic attack becomes larger and breaks armor.";
        codeReleaseFrameLengthsOverride = new List<int>(){1, 1, 1, 1, 1, 1};
        spawnOffsetX = 15;
        //spawnOffsetY = 0;
    }
    public override void LoadSpell()
    {
        base.LoadSpell();
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
            // owner.basicSpawnOverride = spellName;
            // basicEnhanceActive = true;
            SetBasicEnhancement();
            owner.basicSpawnOverrideVariant = (byte)(doesCrit?1:0);
            // Reset the activate flag
            activateFlag = false;
            

            
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
                this.ResolveCrit(owner);
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[doesCrit?9:8].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(0), Fixed.FromInt(spawnOffsetY)));
                string critText = doesCrit?"CRIT":"NON-CRIT";
                owner.SpawnToast($"{critText} LOADED", doesCrit?Color.cyan:GameManager.colors["grey"]);
                break;
            case ProcCondition.OnCastBasic:
            
            if (owner.basicSpawnOverride == spellName && basicEnhanceActive)
                {
                    basicEnhanceActive = false;
                    doesCrit = owner.basicSpawnOverrideVariant == 1;
                    owner.vSpd = Fixed.FromInt(3); // Launch the player upwards slightly
                    owner.hSpd = owner.facingRight ? Fixed.FromInt(-4) : Fixed.FromInt(4); // Propel the player backwatds slightly
                    if (doesCrit)
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-2)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-1)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY+1)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[4].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY + 2)));

                        //Play the Critical Cast VFX
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CRITICAL_CAST, new FixedVec2(owner.position.X + Fixed.FromInt(spawnOffsetX), owner.position.Y + Fixed.FromInt(spawnOffsetY)), owner.pID, owner.facingRight);

                        //Play the Critical Cast SFX
                        SFX_Manager.Instance.PlaySound(Sounds.CRITICAL_CAST);
                    }
                    else
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[5].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY-1)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[6].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[7].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY+1)));
                    }
                }
                break;
            case ProcCondition.ActiveOnHit:
                if (doesCrit)
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
        // AlwaysCrit is an auto-property from IBigStoxActiveSpell, so its backing field was never
        // serialized. EnableForcedCrit/DisableForcedCrit make it real sim state and ResolveCrit reads
        // it, so a rollback that did not restore it could flip a crit outcome.
        bw.Write(((IBigStoxActiveSpell)this).AlwaysCrit);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
        ((IBigStoxActiveSpell)this).AlwaysCrit = br.ReadBoolean();
    }
}

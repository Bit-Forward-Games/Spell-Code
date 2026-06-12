using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class UseTheCredit : SpellData
{
    public bool doesCrit = false;
    public UseTheCredit()
    {
        spellName = "Use The Card";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 120;
        spellInput = 0b_0000_0000_0000_0000_0000_0001_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit};
        description = "Short-range credit swipe.\nHits twice on \"Crit\"<sprite name=\"StockStability\">.";
        projectilePrefabs = new GameObject[2];
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


            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[doesCrit?1:0].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                //if the spell will crit,...
                if(doesCrit)
                {
                    //Play the Critical Cast VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CRITICAL_CAST, new FixedVec2(owner.position.X + Fixed.FromInt(spawnOffsetX), owner.position.Y + Fixed.FromInt(spawnOffsetY)), owner.pID, owner.facingRight);

                    //Play the Critical Cast SFX
                    SFX_Manager.Instance.PlaySound(Sounds.CRITICAL_CAST);
                }
            }
            cooldownCounter = vibeCasted?cooldown+60:cooldown;
            if(vibeCasted) owner.SpawnToast("VIBE CODED", GameManager.colors["grey"]);
            vibeCasted = false;
        }
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

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                doesCrit = GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified;
                break;
            case ProcCondition.ActiveOnHit:
                if (doesCrit && !defender.hitboxData.ignoreEffectDamage && IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
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

using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CoinToss : SpellData
{
    public bool doesCrit = false;
    public CoinToss()
    {
        spellName = "Coin Toss";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_1101_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit};
        description = "Long-range arching coin.\n50% chance of dealing increased damage when missing a \"Crit\"<sprite name=\"StockStability\">.";
        projectilePrefabs = new GameObject[3];
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
            byte projectileIndex = (byte)(doesCrit ? 0 : (GameManager.Instance.GetNextRandom(0, 100) < 50?1:2));

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

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
                int roll = GameManager.Instance.GetNextRandom(0, 100);
                //Debug.Log($"[COINTOSS SYNC] Frame={GameManager.Instance.frameNumber} roll={roll} randomCallCount={GameManager.Instance.randomCallCount}");
                doesCrit = roll < owner.stockStabilityModified;
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

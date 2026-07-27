using UnityEngine;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class LoadedDice : SpellData
{
    public bool doesCrit = false;
    public ushort storedStockStability = 0;
    public const ushort stockStabilityProvidedAmount = 50;
    public LoadedDice()
    {
        spellName = "Loaded Dice";
        brands = new Brand[] { Brand.BigStox };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_0010_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] {ProcCondition.ActiveOnCast, ProcCondition.ActiveOnHit, ProcCondition.OnCastSpell};
        description = "Short range lingering dice roll.\nUnique effects on each number.\nOn Crit<sprite name=\"StockStability\">, throw a loaded die which only results in high values.";
        projectilePrefabs = new GameObject[9];
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
            byte projectileIndex = (byte)(doesCrit ? GameManager.Instance.GetNextRandom(6, 8) : GameManager.Instance.GetNextRandom(0, 5));

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
            cooldownCounter = owner.vibeCoding?(int)(cooldown+((spellInput & 0xFu)*30)):cooldown;
        }
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        doesCrit = false;
        storedStockStability = 0;
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                int roll = GameManager.Instance.GetNextRandom(0, 100);
                doesCrit = roll < owner.stockStabilityModified;
                break;
            case ProcCondition.ActiveOnHit:
                if (projectileInstances[0].activeSelf)
                {
                    owner.demonAura = (ushort)Mathf.Clamp(owner.demonAura + 20, 0, PlayerController.maxDemonAura);
                    owner.SpawnToast("+20 DEMON AURA", GameManager.colors["red"]);
                }
                else if(projectileInstances[1].activeSelf)
                {
                    owner.flowState = (ushort)Mathf.Min(owner.flowState + 240,FlowState.maxFlowState);
                    owner.SpawnToast($"+{4}SEC FLOW STATE", GameManager.colors["green"]);
                }
                else if(projectileInstances[2].activeSelf)
                {
                    owner.reps++;
                    owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                }
                //Mini BCC proc
                else if(projectileInstances[3].activeSelf || projectileInstances[6].activeSelf)
                {
                    storedStockStability += stockStabilityProvidedAmount;
                    owner.stockStability += stockStabilityProvidedAmount;
                    owner.stockStabilityModified = owner.stockStability;
                    if(owner.stockStability > 100)
                    {
                        int excessStocStability = owner.stockStability - 100;
                        owner.stockStability = 100;
                        owner.stockStabilityModified = owner.stockStability;
                        storedStockStability -= (ushort)excessStocStability;

                    }

                    //play the Blue Chip Trader SFX
                    SFX_Manager.Instance.PlaySpellcodeSound("Blue Chip Trader");

                    owner.SpawnToast($"+{stockStabilityProvidedAmount}% STOCK STABILITY", GameManager.colors["blue"]);
                }
                else if(projectileInstances[4].activeSelf || projectileInstances[7].activeSelf)
                {
                    owner.SpawnToast($"CRIT!!", GameManager.colors["blue"]);
                    defender.TakeEffectDamage(StockStability.bigStoxCritDamage,owner, GameManager.colors["blue"]);
                }
                else if(projectileInstances[5].activeSelf || projectileInstances[8].activeSelf)
                {
                    
                    owner.SpawnToast($"SUPER CRIT!!!", GameManager.colors["blue"]);
                    defender.TakeEffectDamage(StockStability.bigStoxCritDamage*3,owner, GameManager.colors["blue"]);
                }

                break;
            case ProcCondition.OnCastSpell:
                //this is for the Mini Bluechip proc
                if(storedStockStability > 0)
                {
                    owner.SpawnToast($"RESET STOCK STABILITY", GameManager.colors["grey"]);
                    owner.stockStability -= storedStockStability;
                    owner.stockStabilityModified = owner.stockStability;
                    storedStockStability = 0;
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
        bw.Write(storedStockStability);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
        storedStockStability = br.ReadUInt16();
    }

    
}

using System;
using System.Collections.Generic;
using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class TheJokah : SpellData
{
    public List<SpellData> JokahVWaveSpells;
    public List<SpellData> JokahBigStoxSpells;
    public bool isVWave = false;
    public TheJokah()
    {
        spellName = "The Jokah";
        brands = new Brand[]{ Brand.DarkWeb, Brand.BigStox, Brand.VWave };
        cooldown = 360;
        spellInput = 0b_0000_0000_0000_0111_1001_1110_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.OnStart, ProcCondition.ActiveOnCast, ProcCondition.OnUpdate, ProcCondition.OnHitSpell, ProcCondition.OnSweetSpot};
        projectilePrefabs = new GameObject[2];
        description = "Casts and enhanced version of one of your VWave<sprite name=\"FlowState\"> or BigStox<sprite name=\"StockStability\"> Spellcodes at random. Hitting Sweet-Spots<sprite name=\"FlowState\"> of other Spellcodes now Crit<sprite name=\"StockStability\">.";
        spawnOffsetX = 0;
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
            byte projectileIndex = (byte)(isVWave?0:1);

            // Instantiate the projectile prefab at the player's position
            // Assuming you have a reference to the player GameObject
            if (owner != null && projectilePrefabs.Length > 1)
            {
                ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), owner.facingRight, new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));

                //if spawning the BigStox jokah
                if(projectileIndex == 1)
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
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnStart:
                JokahVWaveSpells = new List<SpellData>();
                JokahBigStoxSpells = new List<SpellData>();

                foreach(SpellData spell in owner.spellList)
                {
                    if(spell.spellType == SpellType.Active)
                    {
                        List<SpellData> targetList = null;
                        switch (spell.brands[0])
                        {
                            case Brand.BigStox:
                                targetList = JokahBigStoxSpells;
                                break;
                            case Brand.VWave:
                                targetList = JokahVWaveSpells;
                                break;
                        }

                        if (targetList != null)
                        {
                            SpellData spellCopy = Instantiate(spell);
                            spellCopy.owner = owner;
                            spellCopy.projectileInstances.Clear();

                            if (spellCopy is IBigStoxActiveSpell bigStoxCopy)
                            {
                                bigStoxCopy.EnableForcedCrit();
                            }

                            spellCopy.projectilePrefabs = new GameObject[spell.projectilePrefabs.Length];

                            for (int i = 0; i < spell.projectilePrefabs.Length; i++)
                            {
                                GameObject projectileCopy = Instantiate(spell.projectilePrefabs[i]);

                                
                                projectileCopy.GetComponent<BaseProjectile>().LoadProjectile();
                                ProjectileManager.Instance.projectilePrefabs.Add(projectileCopy.GetComponent<BaseProjectile>());
                                projectileCopy.GetComponent<BaseProjectile>().owner = owner;
                                projectileCopy.GetComponent<BaseProjectile>().ownerSpell = spellCopy;
                                spellCopy.projectileInstances.Add(projectileCopy);
                                //color the spell all red if vwave
                                if(projectileCopy.GetComponent<BaseProjectile>().ownerSpell.brands[0] == Brand.VWave)
                                {
                                    projectileCopy.GetComponent<SpriteRenderer>().color = GameManager.colors["red"];
                                }
                                projectileCopy.SetActive(false);
                            }

                            targetList.Add(spellCopy);
                        }
                    }
                }
                break;
            case ProcCondition.ActiveOnCast:
                
                isVWave = GameManager.Instance.GetNextRandom(0, 100) < 50;
                if (isVWave)  //if vwave jokah
                {
                    if (JokahVWaveSpells.Count > 0)
                    {
                        SpellData spell = JokahVWaveSpells[GameManager.Instance.GetNextRandom(0, JokahVWaveSpells.Count)];
                        spell.activateFlag = true;
                        spell.CheckCondition(null, ProcCondition.ActiveOnCast);
                        spell.SpellUpdate();
                        spell.cooldownCounter = 0;
                    }
                }
                else //if bigstox jokah
                {
                    if (JokahBigStoxSpells.Count > 0)
                    {
                        SpellData spell = JokahBigStoxSpells[GameManager.Instance.GetNextRandom(0, JokahBigStoxSpells.Count)];
                        spell.activateFlag = true;
                        spell.CheckCondition(null, ProcCondition.ActiveOnCast);
                        spell.SpellUpdate();
                        spell.cooldownCounter = 0;
                    }
                }
                break;
            case ProcCondition.OnHitSpell:
                if (JokahVWaveSpells.Contains(defender.hitboxData.parentProjectile.ownerSpell))
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnSweetSpot, defender);
                }
                break;
            case ProcCondition.OnSweetSpot:
                if (!JokahVWaveSpells.Contains(defender.hitboxData.parentProjectile.ownerSpell))
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
        bw.Write(isVWave);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        isVWave = br.ReadBoolean();
    }

    private void OnDestroy()
    {
        DestroyCreatedSpells(JokahVWaveSpells);
        DestroyCreatedSpells(JokahBigStoxSpells);
    }

    private static void DestroyCreatedSpells(List<SpellData> spells)
    {
        if (spells == null)
        {
            return;
        }

        for (int i = 0; i < spells.Count; i++)
        {
            SpellData spell = spells[i];
            if (spell == null)
            {
                continue;
            }

            for (int j = 0; j < spell.projectileInstances.Count; j++)
            {
                GameObject projectileObject = spell.projectileInstances[j];
                if (projectileObject == null)
                {
                    continue;
                }

                BaseProjectile projectile = projectileObject.GetComponent<BaseProjectile>();
                if (ProjectileManager.Instance != null)
                {
                    ProjectileManager.Instance.projectilePrefabs.Remove(projectile);
                    ProjectileManager.Instance.activeProjectiles.Remove(projectile);
                }

                Destroy(projectileObject);
            }

            spell.projectileInstances.Clear();
            Destroy(spell.gameObject);
        }

        spells.Clear();
    }
}

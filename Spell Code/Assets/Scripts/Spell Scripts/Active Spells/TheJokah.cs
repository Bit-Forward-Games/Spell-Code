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
        procConditions = new ProcCondition[] { ProcCondition.OnStart, ProcCondition.ActiveOnCast, ProcCondition.OnUpdate, ProcCondition.OnCastBasic, ProcCondition.OnHitSpell, ProcCondition.OnSweetSpot};
        projectilePrefabs = new GameObject[2];
        description = "Casts an enhanced version of one of your VWave<sprite name=\"FlowState\"> or BigStox<sprite name=\"StockStability\"> Spellcodes at random. Hitting Sweet-Spots<sprite name=\"FlowState\"> of other Spellcodes now Crit<sprite name=\"StockStability\">.";
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
                RebuildCreatedSpells();
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
            case ProcCondition.OnCastBasic:
                ForwardPendingSickleBasic(defender);
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

    /// <summary>
    /// Recreates the enhanced spell copies derived from the owner's current inventory. Each copy
    /// retains the original prefab templates so a global pool rebuild can recreate its runtime
    /// projectiles without rerunning OnStart or resetting serialized spell state.
    /// </summary>
    public void RebuildCreatedSpells()
    {
        ClearCreatedSpells();
        JokahVWaveSpells = new List<SpellData>();
        JokahBigStoxSpells = new List<SpellData>();

        if (owner == null || owner.spellList == null)
        {
            return;
        }

        foreach (SpellData spell in owner.spellList)
        {
            if (spell == null
                || spell.spellType != SpellType.Active
                || spell.brands == null
                || spell.brands.Length == 0)
            {
                continue;
            }

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

            if (targetList == null)
            {
                continue;
            }

            SpellData spellCopy = Instantiate(spell);
            spellCopy.owner = owner;
            spellCopy.projectileInstances = new List<GameObject>();

            if (spellCopy is IBigStoxActiveSpell bigStoxCopy)
            {
                bigStoxCopy.EnableForcedCrit();
            }

            GameObject[] sourceProjectilePrefabs = spell.projectilePrefabs ?? Array.Empty<GameObject>();
            spellCopy.projectilePrefabs = new GameObject[sourceProjectilePrefabs.Length];

            bool hasValidProjectileTemplates = true;
            for (int i = 0; i < sourceProjectilePrefabs.Length; i++)
            {
                GameObject projectileTemplate = sourceProjectilePrefabs[i];
                // Store the persistent source asset, never the runtime object that a pool rebuild
                // destroys. This was the missing half of rebuilding Jokah's extra spells.
                spellCopy.projectilePrefabs[i] = projectileTemplate;
                if (projectileTemplate == null)
                {
                    Debug.LogError($"The Jokah: '{spell.spellName}' has a missing projectile prefab at index {i}.");
                    hasValidProjectileTemplates = false;
                }
                else if (projectileTemplate.GetComponent<BaseProjectile>() == null)
                {
                    Debug.LogError($"The Jokah: '{spell.spellName}' projectile prefab at index {i} has no BaseProjectile.");
                    hasValidProjectileTemplates = false;
                }
            }

            // Reject the whole copied spell when one indexed projectile is invalid. Compacting only
            // projectileInstances would make spells that directly use indices 0/1 cast the wrong
            // variant or throw again.
            if (!hasValidProjectileTemplates)
            {
                Destroy(spellCopy.gameObject);
                continue;
            }

            for (int i = 0; i < sourceProjectilePrefabs.Length; i++)
            {
                GameObject projectileCopy = Instantiate(sourceProjectilePrefabs[i]);
                BaseProjectile copiedProjectile = projectileCopy.GetComponent<BaseProjectile>();
                copiedProjectile.LoadProjectile();
                ProjectileManager.Instance.projectilePrefabs.Add(copiedProjectile);
                copiedProjectile.owner = owner;
                copiedProjectile.ownerSpell = spellCopy;
                spellCopy.projectileInstances.Add(projectileCopy);
                ApplyCopiedProjectilePresentation(spellCopy, projectileCopy);
                projectileCopy.SetActive(false);
            }

            targetList.Add(spellCopy);
            owner.extraSpells.Add(spellCopy);
        }
    }

    public static void ApplyCopiedProjectilePresentation(SpellData copiedSpell, GameObject projectileObject)
    {
        if (copiedSpell == null
            || copiedSpell.brands == null
            || copiedSpell.brands.Length == 0
            || copiedSpell.brands[0] != Brand.VWave
            || projectileObject == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = projectileObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && GameManager.colors.TryGetValue("red", out Color enhancedColor))
        {
            spriteRenderer.color = enhancedColor;
        }
    }

    private void ForwardPendingSickleBasic(PlayerController defender)
    {
        if (owner == null || JokahVWaveSpells == null)
        {
            return;
        }

        // Ask the copies which one is armed rather than looking one up by index. This used to read
        // PlayerController.basicSpawnOverrideVariant as an extraSpells index, but SetBasicEnhancement
        // replaced that write and CashOut now stores its crit flag in the same field so the index
        // was either 0 (never forwarded) or 1 from a CashOut cast, which would have forwarded to
        // whatever happened to sit at extraSpells[0]. basicEnhanceActive is per-instance and
        // serialized, so ownership survives rollback.
        for (int i = 0; i < JokahVWaveSpells.Count; i++)
        {
            if (JokahVWaveSpells[i] is SickleOfTheNight enhancedSickle
                && enhancedSickle.OwnsPendingBasicOverride())
            {
                // Route only the armed copied Sickle. Broadcasting OnCastBasic to every extra spell
                // would let both the inventory spell and its Jokah copy react to the same override.
                enhancedSickle.CheckCondition(defender, ProcCondition.OnCastBasic);
                return;
            }
        }
    }

    // ClearPendingCopiedSickleOverride was removed here
    // ClearCreatedSpells now owns teardown, and a lingering basicSpawnOverride string is
    // inert on its own because every consumer also requires that spell's basicEnhanceActive.

    private void OnDestroy()
    {
        ClearCreatedSpells();
    }

    public void ClearCreatedSpells()
    {
        RemoveCreatedSpellsFromOwner(JokahVWaveSpells);
        RemoveCreatedSpellsFromOwner(JokahBigStoxSpells);
        DestroyCreatedSpells(JokahVWaveSpells);
        DestroyCreatedSpells(JokahBigStoxSpells);
    }

    private void RemoveCreatedSpellsFromOwner(List<SpellData> spells)
    {
        if (owner == null || owner.extraSpells == null || spells == null)
        {
            return;
        }

        for (int i = 0; i < spells.Count; i++)
        {
            owner.extraSpells.Remove(spells[i]);
        }
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

            // Unregistering from owner.extraSpells belongs to RemoveCreatedSpellsFromOwner, which
            // ClearCreatedSpells always runs first this method is static and has no owner. The
            // merge spliced a second copy of that removal in here, which is what broke the build.
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
                    // Nulls the pool slot instead of removing it. A List.Remove here renumbered every
                    // projectile after this one, and prefabIndex is the key the rollback savestate and
                    // the projectile hash both use -- so the two machines disagreed about which
                    // projectile a given index meant. This path is reachable from OnDestroy, which
                    // runs on Unity's schedule rather than the sim's, so the renumbering did not even
                    // happen on the same frame on both machines.
                    ProjectileManager.Instance.UnregisterProjectilePrefab(projectile);
                }

                Destroy(projectileObject);
            }

            spell.projectileInstances.Clear();
            Destroy(spell.gameObject);
        }

        spells.Clear();
    }
}

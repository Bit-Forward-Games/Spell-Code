using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class Bailout : SpellData
{
    public bool doesCrit = false;
    public FixedVec2 oldPos = FixedVec2.Zero;
    
    public FixedVec2 newPos = FixedVec2.Zero;

    const int critTrailSpacing = 40;
    const int critTrailStartProjectileIndex = 3;
    const int critTrailEndProjectileIndex = 5;

    bool trailFacingRight = true;
    int critTrailSegmentCount = 0;
    int nextCritTrailSegment = 0;
    int lastCritTrailProjectileIndex = 3;
    public Bailout()
    {
        spellName = "Bailout";
        cooldown = 360;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_0100_1011_0000_0100;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.BigStox };
        projectilePrefabs = new GameObject[6];
        spawnOffsetX = 0;
        spawnOffsetY = 36;
        description = "Longe-range bag toss.\nWhen the bag hits and opponent or the stage, swap places with bag, leaving a burst of money where you were.\nOn \"Crit\"<sprite name=\"StockStability\">, money bursts from your starting point to you.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        doesCrit = false;
        ResetCritTrail();
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
            byte projectileIndex = (byte)(doesCrit ? 1 : 0);

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


    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnCast:
                int roll = GameManager.Instance.GetNextRandom(0, 100);
                doesCrit = roll < owner.stockStabilityModified;
                break;
            case ProcCondition.ActiveOnHit:
                if(defender.hitboxData.parentProjectile.name == projectileInstances[0].name|| defender.hitboxData.parentProjectile.name == projectileInstances[1].name)
                {
                    oldPos = owner.position;
                    newPos = defender.position;
                    trailFacingRight = !defender.facingRight;
                    owner.facingRight = !defender.facingRight;
                    owner.TeleportToDestination(newPos);
                    defender.TeleportToDestination(oldPos);

                    if (doesCrit)
                    {
                        defender.TakeEffectDamage(StockStability.bigStoxCritDamage,owner, GameManager.colors["blue"]);
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[3].GetComponent<BaseProjectile>(), trailFacingRight, GetSpawnOffsetForWorldPosition(GetCritTrailWorldPosition(oldPos)));
                        StartCritTrail();
                    }
                    else
                    {
                        ProjectileManager.Instance.SpawnProjectile(projectileInstances[2].GetComponent<BaseProjectile>(), trailFacingRight, GetSpawnOffsetForWorldPosition(GetCritTrailWorldPosition(oldPos)));

                        ResetCritTrail();
                    }
                }
                
                break;
            case ProcCondition.OnUpdate:
                //handle crit projectileSpawns
                if((oldPos.X != newPos.X || oldPos.Y != newPos.Y)&& doesCrit)
                {
                    UpdateCritTrail();
                }
                break;
            default:
                break;
        }
    }

    void StartCritTrail()
    {
        int sqrDistance = GetSqrDistanceCeilToInt(oldPos, newPos);
        if (sqrDistance <= 0 || projectileInstances.Count <= critTrailStartProjectileIndex)
        {
            ResetCritTrail();
            return;
        }

        critTrailSegmentCount = GetCritTrailSegmentCount(sqrDistance);
        nextCritTrailSegment = 1;
        lastCritTrailProjectileIndex = 3;
    }

    int GetCritTrailSegmentCount(int sqrDistance)
    {
        int segmentCount = 1;
        while (GetSqrCritTrailDistance(segmentCount) < sqrDistance)
        {
            segmentCount++;
        }

        return segmentCount;
    }

    int GetSqrCritTrailDistance(int segmentCount)
    {
        int distance = segmentCount * critTrailSpacing;
        return distance * distance;
    }

    int GetSqrDistanceCeilToInt(FixedVec2 start, FixedVec2 end)
    {
        // Exact integer ceil of the fixed-point deltas, no float. The float version
        // (Mathf.CeilToInt(...ToFloat())) loses precision once the raw delta exceeds float's 24-bit
        // mantissa, and this feeds critTrailSegmentCount, which is serialized+hashed and drives
        // hashed trail-projectile spawns; keep it bit-exact on every platform. ceil(raw/65536) for
        // non-negative raw == (raw + 65535) >> 16, in long to dodge int overflow at the +65535.
        long rawX = Fixed.Abs(end.X - start.X).RawValue;
        long rawY = Fixed.Abs(end.Y - start.Y).RawValue;
        int deltaX = (int)((rawX + 65535) >> 16);
        int deltaY = (int)((rawY + 65535) >> 16);
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    void UpdateCritTrail()
    {
        if (nextCritTrailSegment <= 0 || nextCritTrailSegment > critTrailSegmentCount)
        {
            ResetCritTrail();
            return;
        }

        if (!IsCritTrailProjectileActive(lastCritTrailProjectileIndex))
        {
            return;
        }

        int projectileIndex = GetNextCritTrailProjectileIndex();
        if (projectileIndex < 0)
        {
            ResetCritTrail();
            return;
        }

        FixedVec2 spawnPosition = GetCritTrailWorldPosition(GetCritTrailSegmentPosition(nextCritTrailSegment));
        ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), trailFacingRight, GetSpawnOffsetForWorldPosition(spawnPosition));

        lastCritTrailProjectileIndex = projectileIndex;
        nextCritTrailSegment++;

        if (nextCritTrailSegment > critTrailSegmentCount)
        {
            ResetCritTrail();
        }
    }

    bool IsCritTrailProjectileActive(int projectileIndex)
    {
        if (projectileIndex < 0 || projectileIndex >= projectileInstances.Count || !projectileInstances[projectileIndex].activeSelf)
        {
            return false;
        }

        return projectileInstances[projectileIndex].GetComponent<BaseProjectile>().activeHitboxGroupIndex == 1;
    }

    int GetNextCritTrailProjectileIndex()
    {
        int finalTrailIndex = Mathf.Min(critTrailEndProjectileIndex, projectileInstances.Count - 1);
        if (finalTrailIndex < critTrailStartProjectileIndex)
        {
            return -1;
        }

        int usedTrailCount = finalTrailIndex - critTrailStartProjectileIndex + 1;
        return critTrailStartProjectileIndex + (nextCritTrailSegment % usedTrailCount);
    }

    FixedVec2 GetCritTrailSegmentPosition(int segment)
    {
        Fixed t = Fixed.FromInt(segment) / Fixed.FromInt(critTrailSegmentCount);
        return oldPos + ((newPos - oldPos) * t);
    }

    FixedVec2 GetCritTrailWorldPosition(FixedVec2 position)
    {
        return position + new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY));
    }

    FixedVec2 GetSpawnOffsetForWorldPosition(FixedVec2 worldPosition)
    {
        Fixed xOffset = worldPosition.X - owner.position.X;
        if (!trailFacingRight)
        {
            xOffset *= Fixed.FromInt(-1);
        }

        return new FixedVec2(xOffset, worldPosition.Y - owner.position.Y);
    }

    void ResetCritTrail()
    {
        oldPos = FixedVec2.Zero;
        newPos = FixedVec2.Zero;
        critTrailSegmentCount = 0;
        nextCritTrailSegment = 0;
        lastCritTrailProjectileIndex = 3;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doesCrit);
        bw.Write(oldPos.X.RawValue);
        bw.Write(oldPos.Y.RawValue);
        bw.Write(newPos.X.RawValue);
        bw.Write(newPos.Y.RawValue);
        bw.Write(critTrailSegmentCount);
        bw.Write(nextCritTrailSegment);
        bw.Write(lastCritTrailProjectileIndex);
        // Rollback-critical like the trail counters above, set on the hit frame but read on LATER
        // frames by UpdateCritTrail (spawn facing + x-mirror in GetSpawnOffsetForWorldPosition). If a
        // rollback crosses the hit, a stale value mirrors the remaining trail spawns -> projectile
        // positions are hashed -> desync.
        bw.Write(trailFacingRight);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
        oldPos = new FixedVec2(new Fixed(br.ReadInt32()), new Fixed(br.ReadInt32())); // Assuming Fixed32 uses int
        newPos = new FixedVec2(new Fixed(br.ReadInt32()), new Fixed(br.ReadInt32())); // Assuming Fixed32 uses int
        critTrailSegmentCount = br.ReadInt32();
        nextCritTrailSegment = br.ReadInt32();
        lastCritTrailProjectileIndex = br.ReadInt32();
        trailFacingRight = br.ReadBoolean();
    }
    
}

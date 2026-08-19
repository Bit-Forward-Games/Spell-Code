using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class HotStreak : SpellData
{
    private bool doesCrit = false;
    private const short timeUntilSelfCast = 60;
    private short selfCastCounter = -1;
    public FixedVec2 PlayerCenterOffset;
    private short hotStreakSourcePID = -1;

    public const int rangeThreshold = 150;
    private const int RangeIndicatorSegments = 96;
    private const float RangeIndicatorLineWidth = 1.5f;
    private LineRenderer rangeIndicator;

    public HotStreak()
    {
        spellName = "Hot Streak";
        cooldown = 1;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[] { ProcCondition.OnCrit, ProcCondition.OnUpdate, ProcCondition.ActiveOnHit };
        brands = new Brand[1] { Brand.BigStox };
        projectilePrefabs = new GameObject[2];
        description = "On Crit<sprite name=\"StockStability\">, launch a chain reaction to the nearest other opponent. This Spellcode can Crit<sprite name=\"StockStability\">.";
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnCrit:
                doesCrit = GameManager.Instance.GetNextRandom(0, 100) < owner.stockStabilityModified;
                selfCastCounter = timeUntilSelfCast;
                hotStreakSourcePID = defender.pID == owner.pID? (short)0 : defender.pID;
                break;
            case ProcCondition.ActiveOnHit:
                if (defender.hitboxData.parentProjectile is HotStreakCrit_prj &&
                    IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile))
                {
                    owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnCrit, defender);
                }
                break;
            case ProcCondition.OnUpdate:
                PlayerController hotStreakSource = GetHotStreakSource();

                // Source gone: either the sentinel resolved to nothing, or the player it pointed at
                // left a 3/4P match. Abort the chain rather than deref it this runs from
                // PlayerUpdate inside RunOnlineFrame, so a throw here aborts the frame and the match
                // wedges in a loop that can never confirm
                if (hotStreakSource == null)
                {
                    hotStreakSourcePID = -1;
                    selfCastCounter = -1;
                    UpdateRangeIndicator(false, FixedVec2.Zero);
                    break;
                }

                if(hotStreakSourcePID >= 0)
                {
                    PlayerCenterOffset = new FixedVec2(Fixed.FromInt(0), hotStreakSource.playerHeight/Fixed.FromInt(2));

                }
                if(selfCastCounter == 0)
                {
                    selfCastCounter = -1;
                    int projectileIndex = doesCrit ? 1 : 0;
                    BaseProjectile projectile = projectileInstances[projectileIndex].GetComponent<BaseProjectile>();
                    ProjectileManager.Instance.SpawnProjectile(projectile, hotStreakSource.facingRight, hotStreakSource.position + PlayerCenterOffset, true);
                    SetProjectileTarget(projectile, hotStreakSourcePID);
                }
                else if (selfCastCounter > 0)
                {
                    UpdateRangeIndicator(true, hotStreakSource.position + PlayerCenterOffset);
                    selfCastCounter--;
                    for(short i = 0; i < GameManager.Instance.playerCount; i++)
                    {
                        // players[i] is null for an empty slot in a 2/3P match.
                        PlayerController candidate = GameManager.Instance.players[i];
                        if (candidate == null)
                        {
                            continue;
                        }

                        if (i != owner.pID-1 &&
                            i != hotStreakSourcePID-1 &&
                            IsWithinRadius(candidate.position + PlayerCenterOffset, hotStreakSource.position + PlayerCenterOffset))
                        {
                            selfCastCounter = -1;
                            int projectileIndex = doesCrit ? 1 : 0;
                            BaseProjectile projectile = projectileInstances[projectileIndex].GetComponent<BaseProjectile>();
                            ProjectileManager.Instance.SpawnProjectile(projectile, hotStreakSource.facingRight, hotStreakSource.position + PlayerCenterOffset, true);
                            projectile.playerIgnoreArr[hotStreakSourcePID == 0? owner.pID-1 : hotStreakSourcePID-1] = true;
                            SetProjectileTarget(projectile, (short)(i+1));
                        }
                    }
                }
                else
                {
                    UpdateRangeIndicator(false, FixedVec2.Zero);
                }

                
                
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Resolves the player this chain reaction started from. hotStreakSourcePID uses 0 as a sentinel
    /// for "the owner", set by OnCrit when the crit's defender IS the owner which is exactly what
    /// an Aegis of Athena parry produces, since the reflected projectile comes back into its own
    /// caster. GameManager.GetPlayerByPID reads 0 as "the first NPC" instead, so online (no NPCs) it
    /// returned null and every unguarded deref in OnUpdate threw. The one site that already handled
    /// the sentinel (the playerIgnoreArr index below) confirms 0 was always meant to mean the owner.
    /// </summary>
    private PlayerController GetHotStreakSource()
    {
        if (hotStreakSourcePID == 0)
        {
            return owner;
        }

        if (hotStreakSourcePID < 0)
        {
            return null;
        }

        return GameManager.Instance.GetPlayerByPID(hotStreakSourcePID);
    }

    private static void SetProjectileTarget(BaseProjectile projectile, short targetPID)
    {
        if (projectile is HotStreak_prj normalProjectile)
        {
            normalProjectile.targetPID = targetPID;
        }
        else if (projectile is HotStreakCrit_prj critProjectile)
        {
            critProjectile.targetPID = targetPID;
        }
    }

    private void UpdateRangeIndicator(bool shouldShow, FixedVec2 position)
    {
        if (!shouldShow || owner == null)
        {
            if (rangeIndicator != null)
            {
                rangeIndicator.gameObject.SetActive(false);
            }

            return;
        }

        if (rangeIndicator == null)
        {
            rangeIndicator = CreateRangeIndicator();
        }

        rangeIndicator.gameObject.SetActive(true);
        rangeIndicator.transform.position = new Vector3(position.X.ToFloat(), position.Y.ToFloat(), 0f);
    }
    public bool IsWithinRadius(FixedVec2 targetPosition, FixedVec2 RadiusPosition)
    {
        if (owner == null ) return false;

        
        // Compute squared distance (avoid square root):
        Fixed dx = Fixed.Abs(targetPosition.X - RadiusPosition.X) / Fixed.FromInt(100);
        Fixed dy = Fixed.Abs(targetPosition.Y - RadiusPosition.Y) / Fixed.FromInt(100);
        Fixed distSq = (dx * dx) + (dy * dy);
        Fixed squaredThreshold = Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100) * Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100);

        return distSq < squaredThreshold;
    }

    private LineRenderer CreateRangeIndicator()
    {
        GameObject indicator = new GameObject("Hot Streak Range Indicator");
        LineRenderer lineRenderer = indicator.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = RangeIndicatorSegments;
        lineRenderer.startWidth = RangeIndicatorLineWidth;
        lineRenderer.endWidth = RangeIndicatorLineWidth;
        Color rangeIndicatorColor = GameManager.colors["blue"];
        rangeIndicatorColor.a = 0.5f;
        lineRenderer.startColor = rangeIndicatorColor;
        lineRenderer.endColor = rangeIndicatorColor;
        lineRenderer.sortingLayerName = "GameplayEffects";
        lineRenderer.sortingOrder = 1;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        float radius = rangeThreshold;
        for (int i = 0; i < RangeIndicatorSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RangeIndicatorSegments;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        indicator.SetActive(false);
        return lineRenderer;
    }
    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(doesCrit);
        bw.Write(selfCastCounter);
        bw.Write(hotStreakSourcePID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        doesCrit = br.ReadBoolean();
        selfCastCounter = br.ReadInt16();
        hotStreakSourcePID = br.ReadInt16();
    }
    private void OnDisable()
    {
        UpdateRangeIndicator(false, FixedVec2.Zero);
    }

    private void OnDestroy()
    {
        if (rangeIndicator == null)
        {
            return;
        }

        Destroy(rangeIndicator.gameObject);
        rangeIndicator = null;
    }
}

using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System.Linq;

public class HotStreak : SpellData
{
    private bool doesCrit = false;
    private const short timeUntilSelfCast = 60;
    private short selfCastCounter = 0;

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
        procConditions = new ProcCondition[] { ProcCondition.OnCrit, ProcCondition.OnUpdate};
        brands = new Brand[1] { Brand.BigStox };
        projectilePrefabs = new GameObject[1];
        description = "On Crit<sprite name=\"StockStability\">, launch a chain reaction to the nearest other opponent. This Spellcode can Crit<sprite name=\"StockStability\">.";
    }

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnCrit:
                selfCastCounter = timeUntilSelfCast;
                hotStreakSourcePID = defender.pID;
                break;
            case ProcCondition.OnUpdate:
                if(selfCastCounter == 0)
                {
                    selfCastCounter = -1;
                    int projectileIndex = doesCrit ? 1 : 0;
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), defender.facingRight, GameManager.Instance.GetPlayerByPID(hotStreakSourcePID).position, true);
                    projectileInstances[projectileIndex].GetComponent<HotStreak_prj>().targetPID = hotStreakSourcePID;
                }
                else if (selfCastCounter > 0)
                {
                    UpdateRangeIndicator(true);
                    selfCastCounter--;
                    for(short i = 0; i < GameManager.Instance.playerCount; i++)
                    {
                        if (IsWithinRadius(GameManager.Instance.players[i].position, GameManager.Instance.GetPlayerByPID(hotStreakSourcePID).position))
                        {
                            selfCastCounter = -1;
                            int projectileIndex = doesCrit ? 1 : 0;
                            ProjectileManager.Instance.SpawnProjectile(projectileInstances[projectileIndex].GetComponent<BaseProjectile>(), defender.facingRight, GameManager.Instance.GetPlayerByPID(hotStreakSourcePID).position, true);
                            projectileInstances[projectileIndex].GetComponent<HotStreak_prj>().playerIgnoreArr[hotStreakSourcePID == 0? owner.pID-1 : hotStreakSourcePID-1] = true;
                            projectileInstances[projectileIndex].GetComponent<HotStreak_prj>().targetPID = i;
                        }
                    }
                }
                else
                {
                    UpdateRangeIndicator(false);
                }

                
                
                break;
            default:
                break;
        }
    }

    private void UpdateRangeIndicator(bool shouldShow)
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
        rangeIndicator.transform.position = new Vector3(owner.position.X.ToFloat(), owner.position.Y.ToFloat(), 0f);
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
        UpdateRangeIndicator(false);
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

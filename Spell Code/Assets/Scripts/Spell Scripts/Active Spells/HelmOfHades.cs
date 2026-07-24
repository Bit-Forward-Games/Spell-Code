using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class HelmOfHades : SpellData
{
    //public ushort savedHealth = 255;
    public const int shroudRadius = 125;
    
    private const int RangeIndicatorSegments = 96;
    private const float RangeIndicatorLineWidth = 1.5f;
    private LineRenderer rangeIndicator;
    public HelmOfHades()
    {
        spellName = "Helm Of Hades";
        cooldown = 540;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0000_0000_1110_0000_0010;
        procConditions = new ProcCondition[] { ProcCondition.OnDodge, ProcCondition.OnUpdate };
        brands = new Brand[1] { Brand.Killeez };
        projectilePrefabs = new GameObject[1];
        spawnOffsetX = 0;
        description = "Place down a helmet shrouded in darkness.\nWhile inside the shroud, dodge all attacks from opponents outside the shroud.\nGain 1 Rep<sprite name=\"Reps\"> when you dodge a projectile.";
    }

    
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.OnUpdate:
                // Must run even while the shroud is INACTIVE. UpdateProjectileIgnoreFlags is also the
                // path that CLEARS the flags (ownerIsInsideShroud already tests activeSelf, so it
                // writes false for every projectile when the helmet is down). Gating the call on
                // activeSelf froze playerIgnoreArr at its last value, so a helmet that expired while
                // the owner stood inside it left them dodging every projectile still in flight
                UpdateProjectileIgnoreFlags();

                UpdateRangeIndicator(projectileInstances[0].activeSelf);


                break;
            case ProcCondition.OnDodge:
                //grant the resource
                if(projectileInstances[0].activeSelf)
                {
                    owner.reps++;
                    owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                }
                
                break;
            default:
                break;
        }
    }

    private void UpdateProjectileIgnoreFlags()
    {
        if (owner == null || owner.pID <= 0 || ProjectileManager.Instance == null)
        {
            return;
        }

        int ownerPlayerIndex = owner.pID - 1;

        BaseProjectile shroud = projectileInstances != null &&
            projectileInstances.Count > 0 &&
            projectileInstances[0] != null
            ? projectileInstances[0].GetComponent<BaseProjectile>()
            : null;

        bool ownerIsInsideShroud = shroud != null &&
            shroud.gameObject.activeSelf &&
            IsWithinShroud(owner.position+ new FixedVec2(Fixed.FromInt(0), Fixed.FromInt(spawnOffsetY)), shroud.position);

        foreach (BaseProjectile projectile in ProjectileManager.Instance.projectilePrefabs)
        {
            if (projectile == null ||
                projectile.playerIgnoreArr == null ||
                ownerPlayerIndex >= projectile.playerIgnoreArr.Length)
            {
                continue;
            }

            projectile.playerIgnoreArr[ownerPlayerIndex] =
                ownerIsInsideShroud &&
                projectile.owner != null &&
                !IsWithinShroud(projectile.owner.position + new FixedVec2(Fixed.FromInt(0), Fixed.FromInt(spawnOffsetY)), shroud.position);
        }
    }

    // private static bool IsWithinShroud(FixedVec2 targetPosition, FixedVec2 shroudPosition)
    // {
    //     FixedVec2 offset = targetPosition - shroudPosition;
    //     return offset.SqrMagnitude() <= Fixed.FromInt(shroudRadius * shroudRadius);
    // }
    public bool IsWithinShroud(FixedVec2 targetPosition, FixedVec2 shroudPosition)
    {
        if (owner == null ) return false;

        
        // Compute squared distance (avoid square root):
        Fixed dx = Fixed.Abs(targetPosition.X - shroudPosition.X) / Fixed.FromInt(100);
        Fixed dy = Fixed.Abs(targetPosition.Y - shroudPosition.Y) / Fixed.FromInt(100);
        Fixed distSq = (dx * dx) + (dy * dy);
        Fixed squaredThreshold = Fixed.FromInt(shroudRadius)/ Fixed.FromInt(100) * Fixed.FromInt(shroudRadius)/ Fixed.FromInt(100);

        return distSq < squaredThreshold;
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
        rangeIndicator.transform.position = new Vector3(projectileInstances[0].GetComponent<BaseProjectile>().position.X.ToFloat(), projectileInstances[0].GetComponent<BaseProjectile>().position.Y.ToFloat(), 0f);
    }

    private LineRenderer CreateRangeIndicator()
    {
        GameObject indicator = new GameObject("Helm Of Hades Range Indicator");
        LineRenderer lineRenderer = indicator.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = RangeIndicatorSegments;
        lineRenderer.startWidth = RangeIndicatorLineWidth;
        lineRenderer.endWidth = RangeIndicatorLineWidth;
        Color rangeIndicatorColor = GameManager.colors["purple"];
        rangeIndicatorColor.a = 0.5f;
        lineRenderer.startColor = rangeIndicatorColor;
        lineRenderer.endColor = rangeIndicatorColor;
        lineRenderer.sortingLayerName = "GameplayEffects";
        lineRenderer.sortingOrder = 1;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

        float radius = shroudRadius;
        for (int i = 0; i < RangeIndicatorSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RangeIndicatorSegments;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        indicator.SetActive(false);
        return lineRenderer;
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

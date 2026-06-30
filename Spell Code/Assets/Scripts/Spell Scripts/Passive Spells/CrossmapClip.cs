using UnityEngine;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class CrossmapClip : SpellData
{
    public static int flowStateIncrease = 180;
    public static int rangeThreshold = 200;
    private const int RangeIndicatorSegments = 96;
    private const float RangeIndicatorLineWidth = 1.5f;
    private LineRenderer rangeIndicator;

    public CrossmapClip()
    {
        spellName = "Crossmap Clip";
        cooldown = 60;
        spellType = SpellType.Passive;
        procConditions = new ProcCondition[1] {ProcCondition.OnHit };
        brands = new Brand[1] { Brand.VWave };
        description = $"Dealing Damage from far away grants {flowStateIncrease/60} seconds of Flow State<sprite name=\"FlowState\">.";

    }

    public override void SpellUpdate()
    {
        //basic cooldown handling
        if (cooldownCounter > 0)
        {
            cooldownCounter--;
            UpdateRangeIndicator(false);
            return;
        }

        UpdateRangeIndicator(true);

    }
    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnHit:

                if (cooldownCounter <= 0 && IsFarEnough(defender))
                {
                    owner.SpawnToast($"+{flowStateIncrease/60}SEC FLOW STATE", GameManager.colors["green"]);
                    owner.flowState = (ushort)Mathf.Min(owner.flowState + flowStateIncrease,FlowState.maxFlowState);
                    cooldownCounter = cooldown;
                    UpdateRangeIndicator(false);
                }
                break;
            default:
                break;
        }
    }

    public bool IsFarEnough(PlayerController defender)
    {
        if (owner == null || defender == null) return false;

        
        // Compute squared distance (avoid square root):
        Fixed dx = Fixed.Abs(owner.position.X - defender.position.X) / Fixed.FromInt(100);
        Fixed dy = Fixed.Abs(owner.position.Y - defender.position.Y) / Fixed.FromInt(100);
        Fixed distSq = (dx * dx) + (dy * dy);
        Fixed squaredThreshold = Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100) * Fixed.FromInt(rangeThreshold)/ Fixed.FromInt(100);

        return distSq > squaredThreshold;
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

    private LineRenderer CreateRangeIndicator()
    {
        GameObject indicator = new GameObject("Crossmap Clip Range Indicator");
        LineRenderer lineRenderer = indicator.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = RangeIndicatorSegments;
        lineRenderer.startWidth = RangeIndicatorLineWidth;
        lineRenderer.endWidth = RangeIndicatorLineWidth;
        Color rangeIndicatorColor = GameManager.colors["green"];
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
}

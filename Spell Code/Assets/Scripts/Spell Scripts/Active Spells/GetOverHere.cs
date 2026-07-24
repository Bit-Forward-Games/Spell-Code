using UnityEngine;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GetOverHere : SpellData
{
    private const int _horzLaunchSpeed = 16;
    private const int _vertLaunchSpeed = 6;
    private const int _dartProjectileIndex = 0;
    private const int _firstChainLinkProjectileIndex = 1;
    public GetOverHere()
    {
        spellName = "Get Over Here";
        brands = new Brand[]{ Brand.VWave };
        cooldown = 240;
        spellInput = 0b_0000_0000_0000_0000_0000_0110_0000_0010; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate };
        projectilePrefabs = new GameObject[7];
        description = "Long-range Rope Dart which pulls in opponents.\nHitting the stage with this Spellcode will launch you forward.";
        

    }




    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (projectileInstances == null || projectileInstances.Count <= _dartProjectileIndex)
                {
                    break;
                }

                GetOverHere_prj dart = projectileInstances[_dartProjectileIndex].GetComponent<GetOverHere_prj>();
                if (dart == null)
                {
                    Debug.LogError("GetOverHere missing its dart projectile");
                    break;
                }

                UpdateChainLinks(dart);

                if (dart.collidedWithStage)
                {
                    owner.hSpd = Fixed.FromInt(_horzLaunchSpeed * (owner.position.X < dart.position.X? 1:-1));
                    owner.vSpd = (dart.position.Y -owner.position.Y + Fixed.FromInt(spawnOffsetY))/Fixed.FromInt(10) + Fixed.FromInt( +_vertLaunchSpeed);
                    cooldownCounter = Mathf.Max(cooldownCounter - 60, 0);
                    dart.collidedWithStage = false;
                }
                break;
            default:
                break;
        }
    }

    private void UpdateChainLinks(GetOverHere_prj dart)
    {
        int chainLinkCount = projectileInstances.Count - _firstChainLinkProjectileIndex;
        if (chainLinkCount <= 0)
        {
            return;
        }

        if (!dart.gameObject.activeSelf)
        {
            for (int i = _firstChainLinkProjectileIndex; i < projectileInstances.Count; i++)
            {
                BaseProjectile chainLink = projectileInstances[i].GetComponent<GetOverHereChain_prj>();
                if (chainLink != null && chainLink.gameObject.activeSelf)
                {
                    ProjectileManager.Instance.DeleteProjectile(chainLink);
                }
            }
            return;
        }

        Fixed direction = Fixed.FromInt(dart.facingRight ? 1 : -1);
        FixedVec2 chainStart = owner.position + new FixedVec2(
            Fixed.FromInt(spawnOffsetX) * direction,
            Fixed.FromInt(spawnOffsetY));
        FixedVec2 chainDelta = dart.position - chainStart;
        Fixed spacingDivisor = Fixed.FromInt(chainLinkCount + 1);

        for (int i = 0; i < chainLinkCount; i++)
        {
            BaseProjectile chainLink = projectileInstances[i + _firstChainLinkProjectileIndex].GetComponent<GetOverHereChain_prj>();
            if (chainLink == null)
            {
                continue;
            }

            if (!chainLink.gameObject.activeSelf)
            {
                ProjectileManager.Instance.SpawnProjectile(
                    chainLink,
                    dart.facingRight,
                    new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }

            Fixed t = Fixed.FromInt(i + 1) / spacingDivisor;
            chainLink.position = chainStart + (chainDelta * t);
        }
    }
}

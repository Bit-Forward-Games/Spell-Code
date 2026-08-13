using UnityEngine;
using System.Linq;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpartanBeam : SpellData
{
    //private const int _horzLaunchSpeed = 16;
    //private const int _vertLaunchSpeed = 6;
    private const int _beamEndIndex = 1;
    private const int _firstBeamSegmentProjectileIndex = 2;
    private const int _firstBeamSegmentOffset = 150;
    private const int _beamEndBaseOffset = _firstBeamSegmentOffset/* + 58*/;
    public SpartanBeam()
    {
        spellName = "Spartan Beam";
        brands = new Brand[]{ Brand.DarkWeb, Brand.VWave, Brand.Killeez };
        cooldown = 540;
        spellInput = 0b_0000_0000_0000_1001_0101_0010_0000_0110; // Example input sequence
        spellType = SpellType.Active;
        procConditions = new ProcCondition[] { ProcCondition.OnUpdate, ProcCondition.OnSweetSpot };
        projectilePrefabs = new GameObject[10];
        projIDsToShareHitstop = new ushort[]{0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
        description = "Charge up a massive beam. The range of this beam is determined by your Reps<sprite name=\"Reps\">, doubled when in Flow State<sprite name=\"FlowState\">. Hitting Sweet-Spots<sprite name=\"FlowState\"> grant 1 rep<sprite name=\"Reps\">";
        

    }




    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch (targetProcCon)
        {
            case ProcCondition.OnUpdate:
                if (projectileInstances == null || projectileInstances.Count <= _beamEndIndex)
                {
                    break;
                }

                SpartanBeamEnd_prj beamEnd = projectileInstances[_beamEndIndex].GetComponent<SpartanBeamEnd_prj>();
                SpartanBeamGun_prj gun = projectileInstances[0].GetComponent<SpartanBeamGun_prj>();
                if (beamEnd == null || gun == null)
                {
                    Debug.LogError("Spartan Beam missing its End or Gun projectile");
                    break;
                }
                
                if( projectileInstances[0].activeSelf )
                {
                    owner.hSpd = Fixed.FromInt(0);
                    owner.vSpd = Fixed.FromInt(0);
                    if(gun.logicFrame == gun.animFrames.frameLengths.Take(10).Sum())
                    {
                        int direction = gun.facingRight? 1:-1;
                        ProjectileManager.Instance.SpawnProjectile(beamEnd, gun.facingRight, new FixedVec2(gun.position.X + Fixed.FromInt((_beamEndBaseOffset + owner.reps*15 * (owner.flowState>0?2:1)) * direction), gun.position.Y), true);

                    }

                }

                UpdateBeamSegments(beamEnd);

                break;
            case ProcCondition.OnSweetSpot:
                //only grant resource on the first hit of a multihit per player
                if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile)|| defender.hitboxData.parentProjectile.ownerSpell == this)
                {
                    break;
                }

                //grant the resource
                owner.reps++;
                owner.SpawnToast("+1 Rep", GameManager.colors["yellow"]);
                break;
            default:
                break;
        }
    }

    private void UpdateBeamSegments(SpartanBeamEnd_prj beamEnd)
    {
        int beamSegmentCount = projectileInstances.Count - _firstBeamSegmentProjectileIndex;
        if (beamSegmentCount <= 0)
        {
            return;
        }

        if (!beamEnd.gameObject.activeSelf)
        {
            for (int i = _firstBeamSegmentProjectileIndex; i < projectileInstances.Count; i++)
            {
                BaseProjectile beamSegment = projectileInstances[i].GetComponent<SpartanBeamSegment_prj>();
                if (beamSegment != null && beamSegment.gameObject.activeSelf)
                {
                    ProjectileManager.Instance.DeleteProjectile(beamSegment);
                }
            }
            return;
        }

        Fixed direction = Fixed.FromInt(beamEnd.facingRight ? 1 : -1);
        FixedVec2 beamFirstSegment = owner.position + new FixedVec2(
            Fixed.FromInt(spawnOffsetX + _firstBeamSegmentOffset) * direction,
            Fixed.FromInt(spawnOffsetY));
        FixedVec2 beamSegmentDelta = beamEnd.position - beamFirstSegment;
        Fixed spacingDivisor = Fixed.FromInt(beamSegmentCount + 1);

        for (int i = 0; i < beamSegmentCount; i++)
        {
            BaseProjectile beamSegment = projectileInstances[i + _firstBeamSegmentProjectileIndex].GetComponent<SpartanBeamSegment_prj>();
            if (beamSegment == null)
            {
                continue;
            }

            if (!beamSegment.gameObject.activeSelf)
            {
                ProjectileManager.Instance.SpawnProjectile(
                    beamSegment,
                    beamEnd.facingRight,
                    new FixedVec2(Fixed.FromInt(spawnOffsetX), Fixed.FromInt(spawnOffsetY)));
            }

            Fixed t = Fixed.FromInt(i + 1) / spacingDivisor;
            beamSegment.position = beamFirstSegment + (beamSegmentDelta * t);
        }
    }
}

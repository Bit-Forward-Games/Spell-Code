using UnityEngine;
using BestoNet.Types;
using System.Linq;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using YamlDotNet.Core;

public class ChainsOfThanatos : SpellData
{
    short markedOpponentPID = -1;
    public ChainsOfThanatos()
    {
        spellName = "Chains Of Thanatos";
        cooldown = 540;
        spellType = SpellType.Active;
        spellInput = 0b_0000_0000_0000_0110_0001_1011_0000_0110;
        procConditions = new ProcCondition[] { ProcCondition.ActiveOnHit, ProcCondition.OnUpdate, ProcCondition.OnKill, ProcCondition.OnHit, ProcCondition.OnRankUp };
        brands = new Brand[] { Brand.DarkWeb, Brand.DemonX, Brand.Killeez };
        projectilePrefabs = new GameObject[2];
        description = "Short-range chain attack. On-hit, chain the opponent's soul while your Demon Aura<sprite name=\"DemonAura\"> is Rank C or Above. While chained, the opponent can not use Spellcodes. On Demon Aura<sprite name=\"DemonAura\"> Rank Up, gain 1 Rep<sprite name=\"Reps\">.";
    }

    public override void LoadSpell()
    {
        base.LoadSpell();
        markedOpponentPID = -1;
    }

 

    public override void CheckCondition(PlayerController defender, ProcCondition targetProcCon)
    {
        switch(targetProcCon)
        {
            case ProcCondition.ActiveOnHit:
                markedOpponentPID = defender.pID;
                PlayerController newlyMarked = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                if (newlyMarked != null)
                {
                    ProjectileManager.Instance.SpawnProjectile(projectileInstances[1].GetComponent<BaseProjectile>(), true, newlyMarked.position, true);
                }
                break;
            case ProcCondition.OnUpdate:
                //handle the marked vfx
                if (projectileInstances[1].activeSelf)
                {
                    // Guard EVERY deref, the marked player can be gone entirely (disconnect in a 3/4P
                    // match -> GetPlayerByPID returns null; stale -1 pID would even index players[-2])
                    // an unguarded deref crashes the sim on every client.
                    PlayerController marked = markedOpponentPID >= 0 ? GameManager.Instance.GetPlayerByPID(markedOpponentPID) : null;
                    if (marked != null)
                    {
                        marked.silenced = true;
                    }

                    if (marked != null && marked.isAlive && owner.demonAura > 0)
                    {
                        projectileInstances[1].GetComponent<BaseProjectile>().position = marked.position;
                    }
                    else
                    {
                        ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                        if (marked != null)
                        {
                            marked.UnSilence();
                        }
                        markedOpponentPID = -1;
                    }
                }
                else
                {
                    if(markedOpponentPID >= 0)
                    {
                        PlayerController staleMarked = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                        if (staleMarked != null)
                        {
                            staleMarked.UnSilence();
                        }
                        markedOpponentPID = -1;
                    }

                }
                break;
            case ProcCondition.OnKill:
                //handle the marked vfx
                if (projectileInstances[1].activeSelf)
                {
                    if (defender.pID == markedOpponentPID)
                    {
                        ProjectileManager.Instance.DeleteProjectile(projectileInstances[1].GetComponent<BaseProjectile>());
                        PlayerController killedMarked = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                        if (killedMarked != null)
                        {
                            killedMarked.UnSilence();
                        }
                        markedOpponentPID = -1;
                    }
                }
                else
                {
                    // No >= 0 check existed here at all, so a kill with nothing marked called
                    // GetPlayerByPID(-1) -- the exact stale-pID case the comment above warns about.
                    if (markedOpponentPID >= 0)
                    {
                        PlayerController unmarked = GameManager.Instance.GetPlayerByPID(markedOpponentPID);
                        if (unmarked != null)
                        {
                            unmarked.UnSilence();
                        }
                    }
                    markedOpponentPID = -1;
                }
                break;
            case ProcCondition.OnHit:
                if (projectileInstances[1].activeSelf)
                {
                    projectileInstances[1].GetComponent<BaseProjectile>().logicFrame = 0;
                }
                break;
            case ProcCondition.OnRankUp:
                //only grant resource on the first hit of a multihit per player
                if(!IsFirstMultiHitAgainstTargetPlayer(defender, defender.hitboxData.parentProjectile)|| defender.hitboxData.parentProjectile.ownerSpell == this)
                {
                    break;
                }

                owner.CheckAllSpellConditionsOfProcCon(owner, ProcCondition.OnRepGain, defender);
            break;
            default:
                break;
        }
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(markedOpponentPID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        markedOpponentPID = br.ReadInt16();
    }
    
}

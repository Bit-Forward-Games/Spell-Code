using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpellCode_Gate : MonoBehaviour
{
    public bool isOpen = false;
    public Animator gateAnimator;
    public int ownerPID;
    Bounds gateBounds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gateBounds = GetComponent<Collider>().bounds;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        {
            SetOpen(isOpen);
            return;
        }

        if (!isOpen)
        {
            CheckGateBroken();
        }
        SetOpen(isOpen);
    }

    public void SimulateOnline(bool isRollback = false)
    {
        if (!isOpen)
        {
            CheckGateBroken(isRollback);
        }

        SetOpen(isOpen);
    }

    public void CheckGateBroken(bool isRollback = false)
    {
        if (gateBounds != null && !isOpen)
        {
            GateCollision(isRollback);
        }
    }


    public void GateCollision(bool isRollback = false)
    {
        if (HasGateBounds())
        {
            HurtboxData gateHurtbox = GetGateHurtbox();
            FixedVec2 gateHurtboxPos = FixedVec2.FromFloat(gateBounds.min.x, gateBounds.min.y);

            foreach (BaseProjectile projectile in ProjectileManager.Instance.activeProjectiles)
            {
                     
                // an active projectile can momentarily be a null entry or have a null owner
                if (projectile == null || projectile.owner == null) continue;

                if (projectile.owner.pID != ownerPID) continue;

                if (HitboxManager.Instance.ProcessSingleProjectileCollisison(projectile, gateHurtbox, gateHurtboxPos, true))
                {
                    if (projectile.ownerSpell == null)
                    {
                        // Play the armor-hit SFX only on real frames; rollback resim revisits this
                        // collision and would otherwise replay the sound every resim
                        if (!isRollback && SFX_Manager.Instance != null)
                        {
                            //play the armor hit sfx
                            SFX_Manager.Instance.PlaySound(Sounds.ARMOR_HIT, 1.0f, 1.0f);
                        }
                        ProjectileManager.Instance.DeleteProjectile(projectile);
                        return;//maybe this should be break? idk this def works tho
                    }

                    isOpen = true;

                        if (!isRollback && SFX_Manager.Instance && VFX_Manager.Instance != null)
                        {
                            //play the armor break sfx
                            SFX_Manager.Instance.PlaySound(Sounds.ARMOR_BREAK, 1.0f, 1.0f);

                            // Play the break effect only on real frames; rollback resim may revisit this event.
                            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.GLASS_BREAK, FixedVec2.FromFloat(gameObject.transform.position.x, gameObject.transform.position.y), ownerPID, projectile.facingRight);
                        }

                        if (!isRollback)
                        {
                            GameManager.Instance?.BroadcastAuthoritativeOnlineStateSnapshot($"gate break P{ownerPID}");
                        }

                        return;
                }

            }

        }
    }

    // Helper to set state and update visuals
    public void SetOpen(bool open)
    {
        isOpen = open;

        if (gateAnimator != null)
        {
            gateAnimator.SetBool("live", !open);
        }
    }

    private HurtboxData GetGateHurtbox()
    {
        return new HurtboxData
        {
            xOffset = 0,
            yOffset = Mathf.RoundToInt(gateBounds.size.y),
            width = Mathf.RoundToInt(gateBounds.size.x),
            height = Mathf.RoundToInt(gateBounds.size.y)
        };
    }

private bool HasGateBounds()
{
    return gateBounds.size.x > 0 && gateBounds.size.y > 0;
}
/// <summary>
/// Get the X Offset for the Hitbox or HurtBox
/// </summary>
/// <param name="hitData"></param>
/// <param name="isRight"></param>
/// <returns></returns>
private int GetAttackerOffsetX(HitboxData hitData, bool isRight)
    {
        //return isRight ? hitData.xOffset : -hitData.xOffset;
        return isRight ? hitData.xOffset : -(hitData.xOffset + hitData.width);
    }

    public void Serialize(BinaryWriter bw)
    {
        bw.Write(isOpen);
    }

    public void Deserialize(BinaryReader br)
    {
        SetOpen(br.ReadBoolean());
    }
}

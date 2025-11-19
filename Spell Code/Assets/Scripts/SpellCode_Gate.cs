using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpellCode_Gate : MonoBehaviour
{
    public bool isOpen = false;
    public Animator gateAnimator;
    Bounds gateBounds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gateBounds = GetComponent<Collider>().bounds;
    }

    // Update is called once per frame
    void Update()
    {
        CheckGateBroken();
    }

    public void CheckGateBroken()
    {
        if (gateBounds != null && !isOpen)
        {
            GateCollision();
        }
    }


    public void GateCollision()
    {
        if (gateBounds != null)
        {
            foreach (BaseProjectile projectile in ProjectileManager.Instance.activeProjectiles)
            {
                if(projectile.ownerSpell == null) break;
                if (projectile.projectileHitboxes.Length == 0) break;
                HitboxGroup activeGroup = projectile.projectileHitboxes[projectile.activeHitboxGroupIndex];
                // Combine all hitbox lists into one sequence
                var activeProjHit = activeGroup.hitbox1
                    .Concat(activeGroup.hitbox2)
                    .Concat(activeGroup.hitbox3)
                    .Concat(activeGroup.hitbox4);


                foreach (HitboxData hitbox in activeProjHit)
                {
                    if (CheckCollision(hitbox, projectile.position, gateBounds,
                                projectile.facingRight))
                    {
                        isOpen = true;
                        gateAnimator.SetBool("live", false);
                    }
                }

            }
        }
    }

    private bool CheckCollision(HitboxData hitbox, FixedVec2 hitboxOrigin, Bounds colliderBounds, bool hitboxOwnerFacingRight)
    {

        // If either box has no width or height, return false
        if (colliderBounds.extents.x == 0 || colliderBounds.extents.y == 0)
        {
            return false;
        }
        // Construct Hitbox Boundaries
        Fixed hitboxLeft = hitboxOrigin.X + Fixed.FromInt(GetAttackerOffsetX(hitbox, hitboxOwnerFacingRight));
        Fixed hitboxRight = hitboxOrigin.X + Fixed.FromInt(GetAttackerOffsetX(hitbox, hitboxOwnerFacingRight) + hitbox.width);
        Fixed hitboxTop = hitboxOrigin.Y + Fixed.FromInt(hitbox.yOffset);
        Fixed hitboxBottom = hitboxOrigin.Y + Fixed.FromInt(hitbox.yOffset - hitbox.height);

        //Debug.Log($"Hit: Left {hitboxLeft} Right {hitboxRight} Top {hitboxTop} Bottom {hitboxBottom}");

        

        //Debug.Log($"Hurt: Left {hurtboxLeft} Right {hurtboxRight} Top {hurtboxTop} Bottom {hurtboxBottom}");

        // Check for Collision using AABB
        if (hitboxLeft < Fixed.FromFloat(colliderBounds.max.x) &&
            hitboxRight > Fixed.FromFloat(colliderBounds.min.x) &&
            hitboxTop > Fixed.FromFloat(colliderBounds.min.y) &&
            hitboxBottom < Fixed.FromFloat(colliderBounds.max.y))
        {
            return true;
        }

        return false;
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
}

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;
using DG.Tweening.Core.Easing;

public class Bailout_prj : BaseProjectile
{
    public const int speed = 6;

    public const int arcHeight = 3;
    public bool collidedWithStage = false;
    
    public HurtboxData hurtbox = new HurtboxData
    {
        xOffset = -16,
        yOffset = 16,
        width =32,
        height = 32,
    };
    protected override void InitializeDefaults()
    {
        projName = "Bailout";
        hSpeed = Fixed.FromInt(1);
        vSpeed = Fixed.FromInt(0);
        lifeSpan = 45; 
        fadeOut = true;
        deleteOnHit = true;
        collidedWithStage = false;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 2, 2, 2, 2, 2, 2, 2, 2}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        collidedWithStage = false;
        
        //bounceCount = 0; // reset bounce count on spawn
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * speed); 
        vSpeed = Fixed.FromInt(arcHeight); 
    }
    public override void LoadProjectile()
    {

        //bounceCount = 0;
        deleteOnHit = true;
        
        collidedWithStage = false;
        projectileHitboxes = new HitboxGroup[1];

        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -16,
                    yOffset = 16,
                    width = 32,
                    height = 32,
                    xKnockback = 3,
                    yKnockback = 5,
                    damage = 0,
                    hitstun = 15,
                    attackLvl = 2
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        //CheckStageDataSOCollision();
        if (collidedWithStage)
        {
            hSpeed = Fixed.FromInt(0);
            vSpeed = Fixed.FromInt(0);
            return;
        }
        
        vSpeed -= owner.gravity/Fixed.FromFloat(4f); // Apply gravity to the vertical speed
    }

   
    public void CheckStageDataSOCollision()
    {
        StageDataSO stageDataSO = GameManager.Instance.GetCurrentStageDataSO(); if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
        {
            // if there's no stage or no solids at all, still check platforms below (handled later)
            if (stageDataSO == null) return;
        }

        #region  --- SOLIDS (unchanged behavior) ---
        if (stageDataSO.solidCenter != null && stageDataSO.solidExtent != null)
        {
            int solidCount = Mathf.Min(stageDataSO.solidCenter.Length, stageDataSO.solidExtent.Length);
            if (solidCount > 0)
            {
                Fixed halfW = Fixed.FromInt(hurtbox.width / 2);
                Fixed halfH = Fixed.FromInt(hurtbox.height / 2);

                // projectile AABB
                Fixed pMinX = position.X + hSpeed - halfW;
                Fixed pMaxX = position.X + hSpeed + halfW;
                Fixed pMinY = position.Y + vSpeed;
                Fixed pMaxY = position.Y + vSpeed + Fixed.FromInt(hurtbox.height);

                for (int i = 0; i < solidCount; i++)
                {
                    FixedVec2 center = FixedVec2.FromFloat(stageDataSO.solidCenter[i].x, stageDataSO.solidCenter[i].y);
                    FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.solidExtent[i].x, stageDataSO.solidExtent[i].y);

                    // Treat extent as half-extents: solid min/max
                    FixedVec2 sMin = center - extent;
                    FixedVec2 sMax = center + extent;

                    // Quick rejection test
                    if (pMaxX < sMin.X || pMinX > sMax.X || pMaxY < sMin.Y || pMinY > sMax.Y)
                    {
                        continue;
                    }


                    // Compute penetration amounts
                    Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
                    Fixed overlapY = Fixed.Min(pMaxY, sMax.Y) - Fixed.Max(pMinY, sMin.Y);

                    if (overlapX < Fixed.FromInt(0) || overlapY < Fixed.FromInt(0))
                    {
                        // Numerical edge-case: treat as no collision
                        continue;
                    }
                    //Play the Bailout collision SFX
                    //SFX_Manager.Instance.PlaySpellcodeSound("BailoutCollisionSound");

                    collidedWithStage = true;

                }
            }
        }
        #endregion
        #region --- PLATFORMS (one-way: only collide from above while falling/standing) ---
        // if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
        // {
        //    int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
        //    if (platformCount == 0) return;

        //    Fixed halfW = Fixed.FromInt(hurtbox.width / 2);
        //    Fixed halfH = Fixed.FromInt(hurtbox.height / 2);

        //    // projectile AABB
        //    Fixed pMinX = position.X + hSpeed - halfW;
        //    Fixed pMaxX = position.X + hSpeed + halfW;
        //    Fixed pMinY = position.Y + vSpeed;
        //    Fixed pMaxY = position.Y + vSpeed + Fixed.FromInt(hurtbox.height);

        //    for (int i = 0; i < platformCount; i++)
        //    {
        //        FixedVec2 center = FixedVec2.FromFloat(stageDataSO.platformCenter[i].x, stageDataSO.platformCenter[i].y);
        //        FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.platformExtent[i].x, stageDataSO.platformExtent[i].y);

        //        // Treat extent as half-extents: platform min/max
        //        FixedVec2 sMin = center - extent;
        //        FixedVec2 sMax = center + extent;

        //        // Quick horizontal rejection (platforms only matter when horizontally overlapping)
        //        if (pMaxX < sMin.X || pMinX > sMax.X)
        //        {
        //            continue;
        //        }

        //        // Quick vertical rejection: platforms are thin surfaces; only consider collisions near the top surface.
        //        // We'll only allow collision when the projectile is at or above the platform top and moving downward (or stationary).
        //        // This implements a simple one-way platform behaviour.
        //        Fixed platformTop = sMax.Y;
        //        Fixed platformBottom = sMin.Y;

        //         // If projectile is completely below platform top, ignore.
        //         //    if (pMaxY <= sMin.Y)
        //         //        continue;

        //         // Overlap in X direction
        //         Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
        //         if (overlapX <= Fixed.FromInt(0))
        //             continue;


        //        // Only land on the platform when the projectile's bottom is at or above the platform top (or intersecting it)
        //        // and the projectile is moving downward (vSpd <= 0) or already essentially resting on it.
        //        // This avoids blocking the projectile from jumping up through the platform.
        //        if ((pMinY <= platformTop && position.Y >= platformTop && vSpeed <= Fixed.FromInt(0))||(pMaxY >= platformBottom && position.Y <= platformBottom && vSpeed >= Fixed.FromInt(0)))
        //        {
        //             //Play the Bailout collision SFX
        //             //SFX_Manager.Instance.PlaySpellcodeSound("BailoutCollisionSound");

        //             collidedWithStage = true;
        //        }

        //    }
        // }
        #endregion
    }
    public override void ResetValues()
    {
        base.ResetValues();
        collidedWithStage = false;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(collidedWithStage);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        collidedWithStage = br.ReadBoolean();
    }
}

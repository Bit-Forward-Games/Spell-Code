using BestoNet.Types;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class PongShot_prj : BaseProjectile
{

    public byte bounceCount = 0;
    public byte maxBounces = 8;
    int speed;
    Fixed projectileWidth = Fixed.FromInt(8);
    Fixed projectileHeight = Fixed.FromInt(8);
    public PongShot_prj()
    {
        projName = "PongShot";
        lifeSpan = 240;
        deleteOnHit = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 }, false);
    }

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        bounceCount = 0; // reset bounce count on spawn
        speed = owner.flowState > 0 ? 5 : 4; // Set speed based on flow state
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * speed); // Set horizontal speed based on facing direction
        vSpeed = Fixed.FromInt(-speed); // diagonal movement, so set vertical speed to match horizontal speed
    }
    public override void LoadProjectile()
    {

        deleteOnHit = true;
        bounceCount = 0;
        projectileHitboxes = new HitboxGroup[3];

        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -4,
                    yOffset = 4,
                    width =8,
                    height = 8,
                    xKnockback = 4,
                    yKnockback = 4,
                    damage = 10,
                    hitstun = 20,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[2] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -4,
                    yOffset = 4,
                    width =8,
                    height = 8,
                    xKnockback = 5,
                    yKnockback = 5,
                    damage = 20,
                    hitstun = 30,
                    attackLvl = 2,
                    sweetSpot = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();

        CheckStageDataSOCollision();

        //if the projectile has reached the end of its animation, delete it
        if (logicFrame == animFrames.frameLengths.Take(11).Sum() || logicFrame >= animFrames.frameLengths.Sum()||bounceCount >= maxBounces)
        {
            bounceCount = 0; //reset bounce count for safety in case of any weird edge cases with the projectile sticking around after the end of the animation
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        //if this projectile has bounced twice, turn into the Sweetspot version
        if (bounceCount >= 2 && logicFrame < animFrames.frameLengths.Take(11).Sum())
        {
            logicFrame = animFrames.frameLengths.Take(11).Sum() + 1; //set the logic frame to the start of the end animation
        }


        //determine which hitbox group is active based on the current logic frame
        if (logicFrame > animFrames.frameLengths.Take(3).Sum() && logicFrame < animFrames.frameLengths.Take(11).Sum())
        {
            activeHitboxGroupIndex = 1;
        }
        else if (logicFrame > animFrames.frameLengths.Take(11).Sum())
        {
            activeHitboxGroupIndex = 2;
        }
        else
        {
            activeHitboxGroupIndex = 0;
        }
    }


    public void CheckStageDataSOCollision()
    {
        StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? GameManager.Instance.lobbySO : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
        if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
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
                Fixed halfW = projectileWidth / Fixed.FromInt(2);
                Fixed halfH = projectileHeight / Fixed.FromInt(2);

                // projectile AABB for the *next* frame
                // Calculate potential next position based on current position and velocity
                //FixedVec2 nextPosition = position + new FixedVec2(hSpd, vSpd);
                Fixed pMinX = position.X + hSpeed - halfW;
                Fixed pMaxX = position.X + hSpeed + halfW;
                Fixed pMinY = position.Y + vSpeed;
                Fixed pMaxY = position.Y + vSpeed + projectileHeight;

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

                    // Resolve along the smallest penetration axis
                    if (overlapX < overlapY)
                    {
                        // Resolve horizontally
                        if (position.X < center.X)
                        {
                            // projectile is left of solid -> push left
                            position = new FixedVec2(sMin.X - halfW, position.Y);
                            hSpeed = Fixed.FromInt(-speed);
                        }
                        else
                        {
                            // projectile is right of solid -> push right
                            position = new FixedVec2(sMax.X + halfW, position.Y);
                            hSpeed = Fixed.FromInt(speed);
                        }
                    }
                    else
                    {
                        // Resolve vertically
                        if (position.Y < center.Y)
                        {
                            // projectile is below solid -> push down
                            position = new FixedVec2(position.X, sMin.Y - halfH);
                            vSpeed = Fixed.FromInt(-speed);
                        }
                        else
                        {
                            // projectile is above solid -> land on top
                            position = new FixedVec2(position.X, sMax.Y);
                            vSpeed = Fixed.FromInt(speed);
                        }
                    }

                    bounceCount++; // Increment bounce count when hitting a solid surface
                                   //set logic frame to either the end of the start animation or the end of the end animation, depending on how many times it has bounced, to skip to the appropriate hitbox group
                    if (bounceCount >= 2)
                    {
                        logicFrame = animFrames.frameLengths.Take(11).Sum() + 1; // set to start of end animation
                    }
                    else
                    {
                        logicFrame = animFrames.frameLengths.Take(3).Sum() + 1; // set to start of active hitbox animation
                    }

                }
            }
        }
        #endregion
        #region --- PLATFORMS (one-way: only collide from above while falling/standing) ---
        //if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
        //{
        //    int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
        //    if (platformCount == 0) return;

        //    Fixed halfW = projectileWidth / Fixed.FromInt(2);
        //    Fixed halfH = projectileHeight / Fixed.FromInt(2);

        //    // projectile AABB
        //    Fixed pMinX = position.X + hSpeed - halfW;
        //    Fixed pMaxX = position.X + hSpeed + halfW;
        //    Fixed pMinY = position.Y + vSpeed;
        //    Fixed pMaxY = position.Y + vSpeed + projectileHeight;

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

        //        // If projectile is completely below platform top, ignore.
        //        if (pMaxY <= sMin.Y)
        //            continue;

        //        // Overlap in X direction
        //        Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
        //        if (overlapX <= Fixed.FromInt(0))
        //            continue;


        //        // Only land on the platform when the projectile's bottom is at or above the platform top (or intersecting it)
        //        // and the projectile is moving downward (vSpd <= 0) or already essentially resting on it.
        //        // This avoids blocking the projectile from jumping up through the platform.
        //        if (pMinY <= platformTop && position.Y >= platformTop && vSpeed <= Fixed.FromInt(0))
        //        {
        //            vSpeed = -vSpeed; // Bounce vertically by reversing vertical speed
        //            bounceCount++; // Increment bounce count when hitting a platform
        //                           //set logic frame to either the end of the start animation or the end of the end animation, depending on how many times it has bounced, to skip to the appropriate hitbox group
        //            if (bounceCount >= 2)
        //            {
        //                logicFrame = animFrames.frameLengths.Take(11).Sum() + 1; // set to start of end animation
        //            }
        //            else
        //            {
        //                logicFrame = animFrames.frameLengths.Take(3).Sum() + 1; // set to start of active hitbox animation
        //            }
        //        }

        //    }
        //}
        #endregion
    }
}

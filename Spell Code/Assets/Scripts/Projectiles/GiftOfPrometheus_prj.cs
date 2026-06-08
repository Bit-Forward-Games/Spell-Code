using BestoNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GiftOfPrometheus_prj : BaseProjectile
{

    [NonSerialized] public bool isGrounded = false;
    [NonSerialized] public ushort lifeTime = 0;
    Fixed projectileWidth = Fixed.FromInt(8);
    Fixed projectileHeight = Fixed.FromInt(8);
    protected override void InitializeDefaults()
    {
        projName = "Gift Of Prometheus";
        //ignoreBrand = true;
        multiHitCooldown = 30;
        maxMultiHitCount = 5;
        ignoreEffectDamage = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6}, false);
    }

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        isGrounded = false;
        lifeTime = 0;
        activeHitboxGroupIndex = 0;
        //hSpeed = Fixed.FromInt(0); // Set horizontal speed based on facing direction
        //vSpeed = Fixed.FromInt(0); // diagonal movement, so set vertical speed to match horizontal speed
    }
    public override void LoadProjectile()
    {

        projectileHitboxes = new HitboxGroup[2];

        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -48,
                    yOffset = 64,
                    width =96,
                    height = 64,
                    xKnockback = 0,
                    yKnockback = 0,
                    damage = 1,
                    hitstun = 0,
                    attackLvl = 1
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
        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                animFrames.frameLengths.Take(4).Sum()+1
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(12).Sum()-1
            }
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();

        CheckStageDataSOCollision();
        if (!isGrounded)
        {
            vSpeed -= owner.gravity/Fixed.FromFloat(10f); // Apply gravity to the vertical speed
        }
        if (logicFrame == animFrames.frameLengths.Take(12).Sum())
        {
            logicFrame = animFrames.frameLengths.Take(4).Sum() + 1; //manually loop the animation which we can do bcs this projectile's life is based on the owner's reps
        }
        if (lifeTime == owner.reps*8 + animFrames.frameLengths.Take(4).Sum() + 1 || multiHitCount == 0)
        {
            logicFrame = animFrames.frameLengths.Take(12).Sum() + 1;
            multiHitCount = maxMultiHitCount;
        }
        lifeTime ++;
    }


    public void CheckStageDataSOCollision()
    {
        isGrounded = false; //we set it true here, and will set it to false in the function if at some point the projectile has collided with ground
        StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? (GameManager.Instance.currentStageIndex == -1?GameManager.Instance.lobbySO: (GameManager.Instance.currentStageIndex == -2 ? GameManager.Instance.TutorialSO : GameManager.Instance.trainingGroundsSO)) : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
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
                            hSpeed = Fixed.FromInt(-3);
                            facingRight = false;
                        }
                        else
                        {
                            // projectile is right of solid -> push right
                            position = new FixedVec2(sMax.X + halfW, position.Y);
                            hSpeed = Fixed.FromInt(3);
                            facingRight = true;
                        }
                    }
                    else
                    {
                        // Resolve vertically
                        if (position.Y < center.Y)
                        {
                            // projectile is below solid -> push down
                            position = new FixedVec2(position.X, sMin.Y - halfH);
                            vSpeed = Fixed.FromInt(0);
                        }
                        else
                        {
                            // projectile is above solid -> land on top
                            position = new FixedVec2(position.X, sMax.Y);
                            vSpeed = Fixed.FromInt(0);
                            isGrounded = true;
                        }
                    }
                }
            }
        }
        #endregion
        #region --- PLATFORMS (one-way: only collide from above while falling/standing) ---
        if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
        {
           int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
           if (platformCount == 0) return;

           Fixed halfW = projectileWidth / Fixed.FromInt(2);
           Fixed halfH = projectileHeight / Fixed.FromInt(2);

           // projectile AABB
           Fixed pMinX = position.X + hSpeed - halfW;
           Fixed pMaxX = position.X + hSpeed + halfW;
           Fixed pMinY = position.Y + vSpeed;
           Fixed pMaxY = position.Y + vSpeed + projectileHeight;

           for (int i = 0; i < platformCount; i++)
           {
               FixedVec2 center = FixedVec2.FromFloat(stageDataSO.platformCenter[i].x, stageDataSO.platformCenter[i].y);
               FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.platformExtent[i].x, stageDataSO.platformExtent[i].y);

               // Treat extent as half-extents: platform min/max
               FixedVec2 sMin = center - extent;
               FixedVec2 sMax = center + extent;

               // Quick horizontal rejection (platforms only matter when horizontally overlapping)
               if (pMaxX < sMin.X || pMinX > sMax.X)
               {
                   continue;
               }

               // Quick vertical rejection: platforms are thin surfaces; only consider collisions near the top surface.
               // We'll only allow collision when the projectile is at or above the platform top and moving downward (or stationary).
               // This implements a simple one-way platform behaviour.
               Fixed platformTop = sMax.Y;
               Fixed platformBottom = sMin.Y;

               // If projectile is completely below platform top, ignore.
            //    if (pMaxY <= sMin.Y)
            //        continue;

               // Overlap in X direction
               Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
               if (overlapX <= Fixed.FromInt(0))
                   continue;


               // Only land on the platform when the projectile's bottom is at or above the platform top (or intersecting it)
               // and the projectile is moving downward (vSpd <= 0) or already essentially resting on it.
               // This avoids blocking the projectile from jumping up through the platform.
               if (pMinY <= platformTop && position.Y >= platformTop && vSpeed <= Fixed.FromInt(0))
               {
                    position = new FixedVec2(position.X, platformTop);
                    vSpeed = Fixed.FromInt(0);
                    isGrounded = true;
               }

           }
        }
        #endregion
    }

    public override void ResetValues()
    {
        base.ResetValues();
        isGrounded = false;
        lifeTime = 0;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(isGrounded);
        bw.Write(lifeTime);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        isGrounded = br.ReadBoolean();
        lifeTime = br.ReadUInt16();
    }
}

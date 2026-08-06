using BestoNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class ArmoryOfHephaestusAnvil_prj : BaseProjectile
{

    [NonSerialized] public bool isGrounded = false;
    //[NonSerialized] public ushort lifeTime = 0;
    private const float refGravity = .75f;
    private const ushort baseLifeTime = 60;
    // Fixed projectileWidth = Fixed.FromInt(8);
    // Fixed projectileHeight = Fixed.FromInt(8);
    [NonSerialized] public HurtboxData hurtbox = new HurtboxData
    {
        xOffset = -16*2,
        yOffset = 9*2,
        width =32*2,
        height = 18*2,
    };
    protected override void InitializeDefaults()
    {
        projName = "Armory Of Hephaestus Anvil";
        //ignoreBrand = true;
        lifeSpan = baseLifeTime;
        fadeIn = true;
        fadeOut = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 6, 6, 6, 6, 6, 6, 6, 6}, true);
    }

    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        isGrounded = false;
        
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
                    xOffset = -16*2,
                    yOffset = 9*2,
                    width =32*2,
                    height = 18*2,
                    xKnockback = 1,
                    yKnockback = 4,
                    damage = 15,
                    hitstun = 25,
                    attackLvl = 2,
                    ignoreEffectDamage = true
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
                animFrames.frameLengths.Take(2).Sum()+1
            },
            endFrames = new List<int>
            {
                lifeSpan-10
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
            vSpeed -= Fixed.FromFloat(refGravity);
        }
        else if (logicFrame < lifeSpan-12)
        {
            logicFrame = lifeSpan-12;
        }

        //if this is the start of the looping animation,...
        if (logicFrame == animFrames.frameLengths.Take(5).Sum() + 1)
        {
            //Replay the GoP looping SFX
            SFX_Manager.Instance.PlaySpellcodeSound("Gift Of Prometheus", 1.0f, 1.0f);
        }

    }


    public void CheckStageDataSOCollision()
    {
        isGrounded = false; //we set it true here, and will set it to false in the function if at some point the projectile has collided with ground
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
                // projectile AABB
                Fixed pMinX = position.X + hSpeed + Fixed.FromInt(facingRight?hurtbox.xOffset:(-hurtbox.xOffset-hurtbox.width));
                Fixed pMaxX = position.X + hSpeed + Fixed.FromInt(!facingRight?(-hurtbox.xOffset):(hurtbox.xOffset+hurtbox.width));
                Fixed pMinY = position.Y + vSpeed - Fixed.FromInt(hurtbox.yOffset);
                Fixed pMaxY = position.Y + vSpeed - Fixed.FromInt(hurtbox.yOffset - hurtbox.height);

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
                            position = new FixedVec2(sMin.X - Fixed.FromFloat(hurtbox.width+hurtbox.xOffset), position.Y);
                            hSpeed = Fixed.FromInt(0);
                            facingRight = false;
                        }
                        else
                        {
                            // projectile is right of solid -> push right
                            position = new FixedVec2(sMax.X + Fixed.FromFloat(hurtbox.width+hurtbox.xOffset), position.Y);
                            hSpeed = Fixed.FromInt(0);
                            facingRight = true;
                        }
                    }
                    else
                    {
                        // Resolve vertically
                        if (position.Y < center.Y)
                        {
                            // projectile is below solid -> push down
                            position = new FixedVec2(position.X, sMin.Y - Fixed.FromInt(hurtbox.yOffset));
                            vSpeed = Fixed.FromInt(0);
                        }
                        else
                        {
                            // projectile is above solid -> land on top
                            position = new FixedVec2(position.X, sMax.Y + Fixed.FromInt(hurtbox.height - hurtbox.yOffset));
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

            // projectile AABB
            Fixed pMinX = position.X + hSpeed + Fixed.FromInt(facingRight?hurtbox.xOffset:(-hurtbox.xOffset-hurtbox.width));
            Fixed pMaxX = position.X + hSpeed + Fixed.FromInt(!facingRight?(-hurtbox.xOffset):(hurtbox.xOffset+hurtbox.width));
            Fixed pMinY = position.Y + vSpeed - Fixed.FromInt(hurtbox.yOffset);
            Fixed pMaxY = position.Y + vSpeed - Fixed.FromInt(hurtbox.yOffset - hurtbox.height);

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
                    position = new FixedVec2(position.X, platformTop + Fixed.FromInt(hurtbox.height - hurtbox.yOffset));
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
        //lifeTime = 0;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(isGrounded);
        //bw.Write(lifeTime);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        isGrounded = br.ReadBoolean();
        //lifeTime = br.ReadUInt16();
    }
}

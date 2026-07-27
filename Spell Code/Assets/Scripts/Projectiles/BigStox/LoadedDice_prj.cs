using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class LoadedDice_prj : BaseProjectile
{
    
    public bool isGrounded = false;
    
    Fixed projectileWidth = Fixed.FromInt(44);
    Fixed projectileHeight = Fixed.FromInt(44);
    
    protected override void InitializeDefaults()
    {
        projName = "Loaded Dice";
        hSpeed = Fixed.FromInt(0);
        vSpeed = Fixed.FromInt(0);
        lifeSpan = 0;
        fadeIn = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 6, 6, 6, 6}, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "")
    {
        base.SpawnProjectile(facingRight, spawnOffset, "Loaded Dice");
        activeHitboxGroupIndex = 0;
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * 4); 
        vSpeed = Fixed.FromInt(5); 
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
                    xOffset = -22,
                    yOffset = 22,
                    width = 22*2,
                    height = 22*2,
                    xKnockback = 3,
                    yKnockback = 10,
                    damage = 20,
                    hitstun = 45,
                    attackLvl = 4,
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
                0
            },
            endFrames = new List<int>
            {
                animFrames.frameLengths.Take(12).Sum()
            }
        };
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        CheckStageDataSOCollision();
        
        if (logicFrame == animFrames.frameLengths.Take(12).Sum())
        {
            ProjectileManager.Instance.DeleteProjectile(this);
        }
        

        //this basically checks if the projectile hit something
        if (playerHitArr.Any(ignore => ignore) && !allHitPlayersAreIgnored)
        {
            hSpeed = Fixed.FromInt(0);
            vSpeed = Fixed.FromInt(0);
            activeHitboxGroupIndex = 0;

            playerHitArr = new bool[4] { false, false, false, false };
            logicFrame = animFrames.frameLengths.Take(12).Sum()+1; //set the logic frame to the start of the end animation
        }
        else if(logicFrame <= animFrames.frameLengths.Take(12).Sum())
        {
            if (!isGrounded)
            {
                vSpeed -= owner.gravity; // Apply gravity to the vertical speed
            }
            else
            {
                float slowVal = 1; 
                hSpeed += Fixed.FromFloat(hSpeed > Fixed.FromInt(0) ? -slowVal : slowVal);
                if(hSpeed > Fixed.FromFloat(-.1f) &&hSpeed < Fixed.FromFloat(.1f)) hSpeed = Fixed.FromInt(0);
            }
        }
    }

    public void CheckStageDataSOCollision()
    {
        isGrounded = false; //we set it true here, and will set it to false in the function if at some point the projectile has collided with ground
        StageDataSO stageDataSO = GameManager.Instance.GetCurrentStageDataSO(); 

        if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
        {
            // if there's no stage or no solids at all, still check platforms below (handled later)
            if (stageDataSO == null) return;
        }

        #region  --- SOLIDS ---
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
                Fixed pMinY = position.Y + vSpeed - halfH;
                Fixed pMaxY = position.Y + vSpeed + halfH;

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
                            vSpeed = -vSpeed;
                        }
                        else
                        {
                            // projectile is above solid -> land on top
                            position = new FixedVec2(position.X, sMax.Y + halfH);
                            vSpeed = vSpeed * Fixed.FromFloat(-0.8f);
                            isGrounded = true;
                        }
                    }
                }
            }
        }
        #endregion
        #region --- PLATFORMS ---
        if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
        {
           int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
           if (platformCount == 0) return;

           Fixed halfW = projectileWidth / Fixed.FromInt(2);
           Fixed halfH = projectileHeight / Fixed.FromInt(2);

           // projectile AABB
           Fixed pMinX = position.X + hSpeed - halfW;
           Fixed pMaxX = position.X + hSpeed + halfW;
           Fixed pMinY = position.Y + vSpeed - halfH;
           Fixed pMaxY = position.Y + vSpeed + halfH;

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
               if (pMinY <= platformTop && position.Y -halfH >= platformTop && vSpeed <= Fixed.FromInt(0))
               {
                    position = new FixedVec2(position.X, platformTop + halfH);
                    vSpeed = vSpeed * Fixed.FromFloat(-0.8f);
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
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(isGrounded);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        isGrounded = br.ReadBoolean();
    }
}

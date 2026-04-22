using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Steamworks.ServerList;
using DG.Tweening.Core.Easing;

public class TrickshotAlley_prj : BaseProjectile
{
    public const int slowSpeed = 1;
    public const int fastSpeed = 6;

    public const int highBounce = 6;
    public const int lowBounce = 2;

    public byte bounceCount = 0;
    
    public HurtboxData hurtbox = new HurtboxData
    {
        xOffset = -16,
        yOffset = 16,
        width =32,
        height = 32,
    };
    protected override void InitializeDefaults()
    {
        projName = "Trickshot Alley";
        hSpeed = Fixed.FromInt(1);
        vSpeed = Fixed.FromInt(0);
        lifeSpan = 600; // lasts for 120 logic frames
        deleteOnHit = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,}, false);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset)
    {
        base.SpawnProjectile(facingRight, spawnOffset);
        activeHitboxGroupIndex = 0;
        
        bounceCount = 0; // reset bounce count on spawn
        hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * slowSpeed); 
        vSpeed = Fixed.FromInt(highBounce); 
    }
    public override void LoadProjectile()
    {

        bounceCount = 0;
        deleteOnHit = true;
        projectileHitboxes = new HitboxGroup[2];

        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -12,
                    yOffset = 12,
                    width =24,
                    height = 24,
                    xKnockback = 4,
                    yKnockback = 4,
                    damage = 15,
                    hitstun = 20,
                    attackLvl = 2,
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        projectileHitboxes[1] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>
            {
                new HitboxData
                {
                    xOffset = -12,
                    yOffset = 12,
                    width =24,
                    height = 24,
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
        base.LoadProjectile();
    }

    public override void ProjectileUpdate()
    {
        base.ProjectileUpdate();
        ProcessTrickshotCollisisons();
        //okay so this logic is a bit wonky to understand but basically if the ball hits something,
        //it switches to the non-hitting hitbox group, sets its horizontal speed to 0,
        //and then waits until the animation is done to delete itself.
        if (logicFrame == animFrames.frameLengths.Take(16).Sum() || logicFrame >= animFrames.frameLengths.Sum()|| bounceCount >=6)
        {
            bounceCount = 0; //reset bounce count for safety in case of any weird edge cases with the projectile sticking around after the end of the animation
            ProjectileManager.Instance.DeleteProjectile(this);
        }


        activeHitboxGroupIndex = (byte)(logicFrame > animFrames.frameLengths.Take(16).Sum()? 1:0);
        
        vSpeed -= owner.gravity/Fixed.FromFloat(4f); // Apply gravity to the vertical speed
        // if(owner.flowState > 0 && logicFrame > animFrames.frameLengths.Take(16).Sum())
        // {
        //     CheckStageDataSOCollision();
            
        // }
    }

    public void ProcessTrickshotCollisisons()
    {
        List<BaseProjectile> myProjectiles = ProjectileManager.Instance.activeProjectiles
        .Where(projectile => projectile != null && projectile.owner == owner && projectile != this)
        .ToList();

        foreach(BaseProjectile proj in myProjectiles)
        {
            if(HitboxManager.Instance.ProcessSingleProjectileCollisison(proj, hurtbox, position, facingRight))
            {
                hSpeed = Fixed.FromInt((proj.facingRight ? 1 : -1) * fastSpeed); 
                vSpeed = Fixed.FromInt(lowBounce); 
                logicFrame = animFrames.frameLengths.Take(16).Sum()+1;
            }
        }

    }

    // public void CheckStageDataSOCollision()
    // {
    //     bool enhanced = logicFrame > animFrames.frameLengths.Take(16).Sum();
    //     int speed = enhanced? fastSpeed:slowSpeed;
    //     int vertSpeed = enhanced? lowBounce:highBounce;
    //     StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? GameManager.Instance.lobbySO : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
    //     if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
    //     {
    //         // if there's no stage or no solids at all, still check platforms below (handled later)
    //         if (stageDataSO == null) return;
    //     }

    //     #region  --- SOLIDS (unchanged behavior) ---
    //     if (stageDataSO.solidCenter != null && stageDataSO.solidExtent != null)
    //     {
    //         int solidCount = Mathf.Min(stageDataSO.solidCenter.Length, stageDataSO.solidExtent.Length);
    //         if (solidCount > 0)
    //         {
    //             Fixed halfW = Fixed.FromInt(hurtbox.width/2);
    //             Fixed halfH = Fixed.FromInt(hurtbox.height/2);

    //             // projectile AABB
    //             Fixed pMinX = position.X + hSpeed - halfW;
    //             Fixed pMaxX = position.X + hSpeed + halfW;
    //             Fixed pMinY = position.Y + vSpeed;
    //             Fixed pMaxY = position.Y + vSpeed + Fixed.FromInt(hurtbox.height);

    //             for (int i = 0; i < solidCount; i++)
    //             {
    //                 FixedVec2 center = FixedVec2.FromFloat(stageDataSO.solidCenter[i].x, stageDataSO.solidCenter[i].y);
    //                 FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.solidExtent[i].x, stageDataSO.solidExtent[i].y);

    //                 // Treat extent as half-extents: solid min/max
    //                 FixedVec2 sMin = center - extent;
    //                 FixedVec2 sMax = center + extent;

    //                 // Quick rejection test
    //                 if (pMaxX < sMin.X || pMinX > sMax.X || pMaxY < sMin.Y || pMinY > sMax.Y)
    //                 {
    //                     continue;
    //                 }


    //                 // Compute penetration amounts
    //                 Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
    //                 Fixed overlapY = Fixed.Min(pMaxY, sMax.Y) - Fixed.Max(pMinY, sMin.Y);

    //                 if (overlapX < Fixed.FromInt(0) || overlapY < Fixed.FromInt(0))
    //                 {
    //                     // Numerical edge-case: treat as no collision
    //                     continue;
    //                 }
    //                 bounceCount++;
    //                 logicFrame = enhanced? animFrames.frameLengths.Take(16).Sum()+1:0;
    //                 //Check if owner is in flowstate, and if not, just delete the projectile
    //                 if(owner.flowState <= 0)
    //                 {
    //                     ProjectileManager.Instance.DeleteProjectile(this);
    //                     return;
    //                 }
    //                 // Resolve along the smallest penetration axis
    //                 if (overlapX < overlapY)
    //                 {
    //                     // Resolve horizontally
    //                     if (position.X < center.X)
    //                     {
    //                         // projectile is left of solid -> push left
    //                         position = new FixedVec2(sMin.X - halfW, position.Y);
    //                         hSpeed = Fixed.FromInt(-speed);
    //                     }
    //                     else
    //                     {
    //                         // projectile is right of solid -> push right
    //                         position = new FixedVec2(sMax.X + halfW, position.Y);
    //                         hSpeed = Fixed.FromInt(speed);
    //                     }
    //                 }
    //                 else
    //                 {
    //                     // Resolve vertically
    //                     if (position.Y < center.Y)
    //                     {
    //                         // projectile is below solid -> push down
    //                         position = new FixedVec2(position.X, sMin.Y - halfH);
    //                         vSpeed = Fixed.FromInt(-vertSpeed);
    //                     }
    //                     else
    //                     {
    //                         // projectile is above solid -> land on top
    //                         position = new FixedVec2(position.X, sMax.Y);
    //                         vSpeed = Fixed.FromInt(vertSpeed);
    //                     }
    //                 }

    //             }
    //         }
    //     }
    //     #endregion
    //     #region --- PLATFORMS  ---
    //     if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
    //     {
    //        int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
    //        if (platformCount == 0) return;

    //        Fixed halfW = Fixed.FromInt(hurtbox.width/2);
    //        Fixed halfH = Fixed.FromInt(hurtbox.height/2);

    //        // projectile AABB
    //        Fixed pMinX = position.X + hSpeed - halfW;
    //        Fixed pMaxX = position.X + hSpeed + halfW;
    //        Fixed pMinY = position.Y + vSpeed;
    //        Fixed pMaxY = position.Y + vSpeed + Fixed.FromInt(hurtbox.height);

    //        for (int i = 0; i < platformCount; i++)
    //        {
    //            FixedVec2 center = FixedVec2.FromFloat(stageDataSO.platformCenter[i].x, stageDataSO.platformCenter[i].y);
    //            FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.platformExtent[i].x, stageDataSO.platformExtent[i].y);

    //            // Treat extent as half-extents: platform min/max
    //            FixedVec2 sMin = center - extent;
    //            FixedVec2 sMax = center + extent;

    //            // Quick horizontal rejection (platforms only matter when horizontally overlapping)
    //            if (pMaxX < sMin.X || pMinX > sMax.X)
    //            {
    //                continue;
    //            }

    //            // Quick vertical rejection: platforms are thin surfaces; only consider collisions near the top surface.
    //            // We'll only allow collision when the projectile is at or above the platform top and moving downward (or stationary).
    //            // This implements a simple one-way platform behaviour.
    //            Fixed platformTop = sMax.Y;
    //            Fixed platformBottom = sMin.Y;


    //            // Overlap in X direction
    //            Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
    //            if (overlapX <= Fixed.FromInt(0))
    //                continue;


    //            // Only land on the platform when the projectile's bottom is at or above the platform top (or intersecting it)
    //            // and the projectile is moving downward (vSpd <= 0) or already essentially resting on it.
    //            // This avoids blocking the projectile from jumping up through the platform.
    //            if ((pMinY <= platformTop && position.Y >= platformTop && vSpeed <= Fixed.FromInt(0))||(pMaxY >= platformBottom && position.Y <= platformBottom && vSpeed >= Fixed.FromInt(0)))
    //            {
                    
    //                vSpeed = -vSpeed; // Bounce vertically by reversing vertical speed
    //                //reset the logic frame
    //                logicFrame = enhanced? animFrames.frameLengths.Take(16).Sum()+1:0;
    //                bounceCount++;
    //            }

    //        }
    //     }
    //     #endregion
    // }
    public override void ResetValues()
    {
        base.ResetValues();
        bounceCount = 0;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(bounceCount);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        bounceCount = br.ReadByte();
    }
}

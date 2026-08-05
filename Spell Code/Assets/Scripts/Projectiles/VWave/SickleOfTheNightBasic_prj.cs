using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using System;

public class SickleOfTheNightBasic_prj : BaseProjectile
{
    private const int speed = 3;
    public short targetPID;
    
    protected override void InitializeDefaults()
    {
        projName = "Sickle Of The Night Basic";
        hSpeed = Fixed.FromInt(0);
        vSpeed = Fixed.FromInt(0);
        fadeIn = true;
        fadeOut = true;
        lifeSpan = 90;
        deleteOnHit = true;
        animFrames = new AnimFrames(new List<int>(), new List<int>() { 3, 3, 3, 3}, true);
    }
    
    public override void SpawnProjectile(bool facingRight, FixedVec2 spawnOffset, string nameOverride = "", bool useAbsolutePosition = false)
    {
        base.SpawnProjectile(facingRight, spawnOffset, "", useAbsolutePosition);
        hSpeed = Fixed.FromInt(0); 
        vSpeed = Fixed.FromInt(0); 
        targetPID = ownerSpell.gameObject.GetComponent<SickleOfTheNight>().targetPID;

        if (targetPID >= 0)
        {
            PlayerController cachedTargetPlayer = GameManager.Instance.GetPlayerByPID(targetPID);
            FixedVec2 directionVector = GetDirectionTo(cachedTargetPlayer.position);
            hSpeed = directionVector.X * Fixed.FromInt(speed);
            vSpeed = directionVector.Y * Fixed.FromInt(speed);
        }
        else
        {
            hSpeed = Fixed.FromInt((facingRight ? 1 : -1) * speed);
            vSpeed = Fixed.FromInt(0);
        }
    }

    private FixedVec2 GetDirectionTo(FixedVec2 targetPosition)
    {
        Fixed deltaX = targetPosition.X - position.X;
        Fixed deltaY = targetPosition.Y - position.Y;
        Fixed scale = Fixed.Max(Fixed.Abs(deltaX), Fixed.Abs(deltaY));

        // Fixed32 cannot hold deltaX^2 + deltaY^2 for distances above roughly
        // 181 units. Scale first so Normalized() only squares values in [-1, 1].
        if (scale == Fixed.FromInt(0))
        {
            return FixedVec2.Zero;
        }

        return new FixedVec2(deltaX / scale, deltaY / scale).Normalized();
    }

    public override void LoadProjectile()
    {

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
                    damage = 15,
                    hitstun = 15,
                    attackLvl = 1,
                    basicAttackHitbox = true
                }
            },
            hitbox2 = new List<HitboxData>(),
            hitbox3 = new List<HitboxData>(),
            hitbox4 = new List<HitboxData>()
        };
        
        
        base.LoadProjectile();
    }

    // public override void ProjectileUpdate()
    // {
    //     base.ProjectileUpdate();
    //     if (targetPID >= 0)
    //     {
    //         PlayerController cachedTargetPlayer = GameManager.Instance.GetPlayerByPID(targetPID);
    //         FixedVec2 directionVector = new FixedVec2(cachedTargetPlayer.position.X -position.X, cachedTargetPlayer.position.Y - position.Y).Normalized();
    //         hSpeed = directionVector.X * Fixed.FromInt(speed);
    //         vSpeed = directionVector.Y * Fixed.FromInt(speed);
    //     }
    //     else
    //     {
    //         ProjectileManager.Instance.DeleteProjectile(this);
    //     }
    // }

    public override void ResetValues()
    {
        base.ResetValues();
        targetPID = -1;
    }

    public override void Serialize(System.IO.BinaryWriter bw)
    {
        base.Serialize(bw);
        bw.Write(targetPID);
    }

    public override void Deserialize(System.IO.BinaryReader br)
    {
        base.Deserialize(br);
        targetPID = br.ReadInt16();
    }
}

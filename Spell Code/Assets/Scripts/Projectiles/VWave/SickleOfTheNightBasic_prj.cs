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
        // ownerSpell is serialized as an index into its holder's spellList, and a reflect REASSIGNS
        // a projectile's owner, which is exactly how ownerSpell came back null and threw inside a
        // rollback resim, hard-freezing the match rather than merely desyncing it (a frame that
        // throws can never be confirmed). GetComponent can also come back null if the reassigned
        // owner's spell is not a Sickle of the Night at all.
        // 
        // Degrading to the un-homed straight shot is determinism-safe: ownerSpell, the spell list and
        // the roster are all sim state, so every peer takes the same branch on the same frame.
        SickleOfTheNight sourceSpell = ownerSpell != null
            ? ownerSpell.gameObject.GetComponent<SickleOfTheNight>()
            : null;
        targetPID = sourceSpell != null ? sourceSpell.targetPID : (short)-1;

        // A recorded target can also be gone by now (killed, disconnected), so resolving the player
        // has to be allowed to fail too.
        PlayerController cachedTargetPlayer = targetPID >= 0 && GameManager.Instance != null
            ? GameManager.Instance.GetPlayerByPID(targetPID)
            : null;

        if (cachedTargetPlayer != null)
        {
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

        // Group 0 is the INACTIVE group and groups 1..N map to frameData's windows, because
        // BaseProjectile.ProjectileUpdate sets activeHitboxGroupIndex = (byte)(i + 1) for the
        // matching window and 0 when none match. With one startFrame the index reaches 1, so the
        // array must hold two entries -- sizing it to 1 and putting the hitbox at [0] meant
        // HitboxManager indexed [1] on the first active frame and threw IndexOutOfRangeException
        // inside RunFrame
        projectileHitboxes = new HitboxGroup[2];
        projectileHitboxes[0] = new HitboxGroup
        {
            hitbox1 = new List<HitboxData>(),
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

        frameData = new FrameData
        {
            startFrames = new List<int>
            {
                4
            },
            endFrames = new List<int>
            {
                lifeSpan
            }
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

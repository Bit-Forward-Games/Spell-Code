using UnityEngine;

/// <summary>
/// The bot's neutral game: hold a spacing band against the nearest opponent, jump what needs
/// jumping, and never walk off a ledge.
///
/// Facing comes free: the owner's Idle and Run states both turn toward the held direction before
/// accelerating, so holding 4 or 6 is the entire instruction.
///
/// </summary>
public class ChaseAI : NpcAI
{
    // Distances are in the same units as the stage AABBs and hurtboxes, which are pixels: a
    // character is roughly 20 wide and 48 tall, so this band sits a few body-widths out.
    private const float PreferredDistance = 90f;
    private const float BandWidth = 34f;

    // How far above the owner the target has to be before climbing to it is worth a jump.
    private const float ClimbThreshold = 56f;

    // Room a code needs before it's worth starting, plus the extra each step demands. Entering a
    // code takes frames and CodeRelease recovery grows with its length, so a long code is only
    // affordable from further out.
    private const float MinCastSpace = 40f;
    private const float SpacePerCodeStep = 22f;

    // Past this the bot is out of its own range and a cast is just feeding the opponent.
    private const float MaxCastRange = 260f;

    // Inside this, a target already swinging is a reason to hold rather than commit.
    private const float ThreatRange = 110f;

    // Breathing room between attempts, so a ready spell doesn't get thrown every frame it's up.
    private const int FramesBetweenAttempts = 24;

    private int framesSinceAttempt;

    public override string BehaviorName => "Chase";

    public override void NPCUpdate()
    {
        framesSinceAttempt++;

        if (!HasTarget)
        {
            Neutral();
            return;
        }

        // Attack before moving: once a cast starts the base class owns the inputs for the whole
        // sequence, so there is no point choosing a direction we'd immediately hand over.
        if (TryAttack())
        {
            return;
        }

        int desired = ChooseDirection();

        // Holding neutral never turns the owner, so a bot sitting in the band facing the wrong way
        // would stay that way forever -- and TryAttack refuses to cast unless it's facing the
        // target. Face them instead; the state machine turns before it accelerates.
        if (desired == 5 && !FacingTarget)
        {
            desired = TargetOffsetX > 0f ? 6 : 4;
        }

        // Ledge safety applies only on the ground. Airborne, the same step is how the bot gets back
        // to the stage, so refusing it there would strand anything that ever leaves the floor.
        if (IsGrounded && desired != 5 && !IsStepSafe(desired))
        {
            desired = 5;
        }

        SetDirection(desired);

        if (ShouldJump(desired))
        {
            TapJump();
        }
    }

    /// <summary>
    /// Decides whether to start a code this tick, and starts it if so. Returns true when a cast
    /// began, at which point the base class drives the inputs until the sequence finishes.
    /// </summary>
    private bool TryAttack()
    {
        // Only from a settled position on the ground: entering a code roots the owner in place, so
        // starting one mid-scramble is how a bot gets punished.
        if (!IsGrounded || framesSinceAttempt < FramesBetweenAttempts)
        {
            return false;
        }

        if (TargetDistanceX > MaxCastRange || !FacingTarget)
        {
            return false;
        }

        // A target already committed to their own swing is the wrong moment to start something
        // long. Backing off is handled by the spacing band; here we simply decline.
        bool targetIsSwinging = TargetState == PlayerState.CodeRelease
            || TargetState == PlayerState.CodeWeave;
        if (targetIsSwinging && TargetDistanceX < ThreatRange)
        {
            return false;
        }

        SpellData spell = ShortestReadySpell();
        if (spell != null && HasRoomFor(spell) && BeginCast(spell))
        {
            framesSinceAttempt = 0;
            return true;
        }

        // Nothing affordable: a bare Code press and release still throws the basic attack, which
        // is the right answer at close range anyway.
        if (TargetDistanceX < ThreatRange && BeginBasicAttack())
        {
            framesSinceAttempt = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Longer codes cost more frames to enter and leave more recovery behind, so they are only
    /// worth starting from further away. This is the whole risk model: length is commitment.
    /// </summary>
    private bool HasRoomFor(SpellData spell)
    {
        int length = PlayerController.GetSpellInputLength(spell);
        return TargetDistanceX >= MinCastSpace + (length * SpacePerCodeStep);
    }

    /// <summary>
    /// Closes when the gap is wider than the band, backs off when it's tighter, holds otherwise.
    /// The dead band in the middle is what stops the bot vibrating on the spot.
    /// </summary>
    private int ChooseDirection()
    {
        int toward = TargetOffsetX > 0f ? 6 : 4;
        int away = TargetOffsetX > 0f ? 4 : 6;

        if (TargetDistanceX > PreferredDistance + BandWidth)
        {
            return toward;
        }

        if (TargetDistanceX < PreferredDistance - BandWidth)
        {
            return away;
        }

        return 5;
    }

    private bool ShouldJump(int desiredDirection)
    {
        if (!CanJump)
        {
            return false;
        }

        if (!IsGrounded)
        {
            // Recovery: falling with nothing underneath means the bot is off the stage and its
            // remaining jumps are the only way back.
            return IsFalling && OverAVoid();
        }

        // The target is somewhere above a platform, most likely. Climb toward it.
        if (TargetOffsetY > ClimbThreshold)
        {
            return true;
        }

        // Walled in while trying to move: hop the obstacle instead of grinding into it forever.
        if (desiredDirection == 6)
        {
            return owner != null && owner.touchingRightWall;
        }

        if (desiredDirection == 4)
        {
            return owner != null && owner.touchingLeftWall;
        }

        return false;
    }
}

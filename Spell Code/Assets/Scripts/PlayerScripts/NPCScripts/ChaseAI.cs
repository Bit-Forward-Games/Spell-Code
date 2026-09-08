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

    public override string BehaviorName => "Chase";

    public override void NPCUpdate()
    {
        if (!HasTarget)
        {
            Neutral();
            return;
        }

        int desired = ChooseDirection();

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

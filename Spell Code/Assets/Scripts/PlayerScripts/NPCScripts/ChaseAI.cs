using UnityEngine;
using UnityEngine.SceneManagement;

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

    // How close to stand to the lobby gate before casting at it, so even the shortest-range
    // starting spell connects rather than dying in mid-air.
    private const float GateCastDistance = 60f;

    // The go door counts a player as inside within 36 units; stop a bit tighter than that so a
    // bot settling into place doesn't drift back out and un-ready everyone.
    private const float DoorArrivalRadius = 24f;

    private int framesSinceAttempt;

    public override string BehaviorName => "Chase";

    public override void NPCUpdate()
    {
        framesSinceAttempt++;

        // In the lobby the spell gate walls this slot in until its owner shoots it, so nothing else
        // the bot might want is reachable until it's down. Handle it before anything else.
        SpellCode_Gate gate = OwnGate();
        if (gate != null && !gate.isOpen)
        {
            BreakOwnGate(gate);
            return;
        }

        // Lobby and shop are staging rooms, not fights: the match only starts once every player is
        // stood inside the go door, so head there and wait rather than chasing people around.
        GO_Door goDoor = InStagingScene() ? GameManager.Instance?.goDoorPrefab : null;
        if (goDoor != null)
        {
            MoveToDoor(goDoor);
            return;
        }

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
    /// True in the scenes where the go door decides when the match starts.
    /// goDoorPrefab hangs off the persistent GameManager, so it can't be trusted to go
    /// null in an arena, and a bot walking to a door mid-fight would be a lot worse than this.
    /// </summary>
    private static bool InStagingScene()
    {
        string scene = SceneManager.GetActiveScene().name;
        return scene == "MainMenu" || scene == "Shop";
    }

    /// <summary>
    /// Walks into the go door and stands there. GO_Door.CheckAllPlayersReady requires every
    /// connected player to be inside its radius AND grounded, so arriving means standing still.
    /// </summary>
    private void MoveToDoor(GO_Door door)
    {
        if (owner == null)
        {
            Neutral();
            return;
        }

        float offsetX = door.transform.position.x - owner.position.X.ToFloat();
        float offsetY = door.transform.position.y - owner.position.Y.ToFloat();
        bool alignedX = Mathf.Abs(offsetX) <= DoorArrivalRadius;

        // The door measures both axes, so being level with it matters as much as being beside it.
        if (alignedX && Mathf.Abs(offsetY) <= DoorArrivalRadius)
        {
            Neutral();
            return;
        }

        // TravelTowardX rather than a bare SetDirection: the route to the door has bumps to hop and
        // lips the ledge probe won't vouch for, and plain ledge safety turns both into a dead stop.
        TravelTowardX(door.transform.position.x, DoorArrivalRadius);

        // Lined up underneath a door that sits higher up: climb to it.
        if (alignedX && IsGrounded && offsetY > ClimbThreshold && CanJump)
        {
            TapJump();
        }
    }

    /// <summary>
    /// Walks up to this bot's own lobby gate, faces it, and casts at it until it breaks.
    ///
    /// It has to be a spell, not a basic attack: the gate deletes any incoming projectile whose
    /// ownerSpell is null, so basic attacks bounce off it forever. The Gamba machine would also
    /// block the break while it's active, but it switches itself off as soon as its owner holds a
    /// spell, and bots are handed their starter at spawn -- so by the time one gets here it's off.
    /// </summary>
    private void BreakOwnGate(SpellCode_Gate gate)
    {
        if (owner == null)
        {
            Neutral();
            return;
        }

        float offset = gate.transform.position.x - owner.position.X.ToFloat();
        bool gateIsRight = offset > 0f;
        int toward = gateIsRight ? 6 : 4;

        // Face it first -- a spell projectile comes out in whichever direction the owner faces --
        // and close in if a short-range spell would fall short.
        if (owner.facingRight != gateIsRight)
        {
            SetDirection(toward);
            return;
        }

        if (Mathf.Abs(offset) > GateCastDistance)
        {
            TravelTowardX(gate.transform.position.x, GateCastDistance);
            return;
        }

        Neutral();

        if (!IsGrounded || framesSinceAttempt < FramesBetweenAttempts)
        {
            return;
        }

        SpellData spell = AnyReadySpell();
        if (spell != null && BeginCast(spell))
        {
            framesSinceAttempt = 0;
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

        SpellData spell = ChooseSpell();
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

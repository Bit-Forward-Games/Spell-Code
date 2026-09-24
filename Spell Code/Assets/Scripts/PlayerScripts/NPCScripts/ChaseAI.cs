using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The bot's neutral game: hold a spacing band against the nearest opponent, jump what needs
/// jumping, and follow safe platform routes between levels.
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

    // Height separation at which reaching the target's level takes priority over combat spacing.
    private const float ClimbThreshold = 32f;

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

    // A floppy only registers an overlapping player within 18 units, so stand well inside that.
    private const float FloppyReachRadius = 12f;

    // How far above or below the Gamba's base still counts as its level. Half a body: the disk
    // platform sits a full 80 above it, while the Gamba's own floor is within a couple of units.
    private const float GambaHeightTolerance = 24f;

    private int framesSinceAttempt;

    // How hard a fat bounty pulls a RAM Rush bot off the nearest opponent, per point of bounty
    // against one unit of distance. Bounties run to the hundreds, so this is deliberately small.
    private const float BountyWeight = 0.25f;

    // How hard a nearly-eliminated opponent pulls, per stock they're down.
    private const float StockWeight = 60f;

    /// <summary>
    /// True when losing one more exchange ends this bot's match. Only Elimination has that cliff --
    /// RAM Rush respawns you -- so this is what makes the two modes feel different to play against.
    /// </summary>
    private bool IsOnLastStock()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager != null
            && gameManager.winCon == GameManager.WinCon.Elimination
            && owner != null
            && owner.winConPoints <= 1;
    }

    /// <summary>
    /// Nearest opponent, adjusted for what the mode actually rewards: in RAM Rush the RAM comes out
    /// of whoever you defeat and scales with their bounty, so a fat target is worth walking past a
    /// closer one for. In Elimination there is no bounty, only stocks, so finish whoever is nearest
    /// to being out.
    /// </summary>
    protected override PlayerController SelectTarget()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || owner == null)
        {
            return base.SelectTarget();
        }

        PlayerController best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < gameManager.playerCount; i++)
        {
            PlayerController candidate = gameManager.players[i];
            if (candidate == null || candidate == owner || !candidate.isAlive || !candidate.isConnected)
            {
                continue;
            }

            // Closer is always better; the mode bonus decides how much closer is worth it.
            float score = -Mathf.Abs(candidate.position.X.ToFloat() - owner.position.X.ToFloat());

            if (gameManager.winCon == GameManager.WinCon.RAMRush)
            {
                score += candidate.ramBounty * BountyWeight;
            }
            else if (gameManager.winCon == GameManager.WinCon.Elimination)
            {
                score += (gameManager.WinConPointLimit - candidate.winConPoints) * StockWeight;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    public override string BehaviorName => "Chase";

    public override void NPCUpdate()
    {
        framesSinceAttempt++;

        bool staging = InStagingScene();

        // Shop first, and specifically before the gate: the gate only breaks to a spell projectile,
        // so a bot with an empty spell list has nothing to break it with and would stand there forever. 
        if (staging && HasSpellRoom && TryShop())
        {
            return;
        }

        // The lobby gate walls this slot in until its owner shoots it, so nothing past it is
        // reachable until it's down.
        SpellCode_Gate gate = OwnGate();
        if (gate != null && !gate.isOpen)
        {
            BreakOwnGate(gate);
            return;
        }

        // Lobby and shop are staging rooms, not fights: the match only starts once every player is
        // stood inside the go door, so head there and wait rather than chasing people around.
        GO_Door goDoor = staging ? GameManager.Instance?.goDoorPrefab : null;
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

        // Reach the opponent's level before spacing or casting. Horizontal attack range alone
        // used to keep a bot firing beneath an opponent instead of taking the platform route.
        if (IsNavigating || Mathf.Abs(TargetOffsetY) > ClimbThreshold)
        {
            TravelToward(new Vector2(owner.position.X.ToFloat() + TargetOffsetX,
                owner.position.Y.ToFloat() + TargetOffsetY), 12f, ClimbThreshold);
            return;
        }

        // Attack before moving: once a cast starts the base class owns the inputs for the whole
        // sequence, so there is no point choosing a direction we'd immediately hand over.
        if (TryAttack())
        {
            return;
        }

        int desired = ChooseDirection();
        int away = TargetOffsetX > 0f ? 4 : 6;

        // Ledge safety applies only on the ground. Airborne, the same step is how the bot gets back
        // to the stage, so refusing it there would strand anything that ever leaves the floor.
        if (IsGrounded && desired != 5 && !IsStepSafe(desired))
        {
            desired = 5;
        }

        // A retreat also ends at a wall: pressing on only grinds into it, or hops against it, with
        // the bot's back to its target.
        if (desired == away && WallAhead(away))
        {
            desired = 5;
        }

        // Holding neutral never turns the owner, so a bot sitting in the band facing the wrong way
        // would stay that way forever -- and TryAttack refuses to cast unless it's facing the
        // target. Face them instead; the state machine turns before it accelerates. This comes after
        // the ledge and wall checks on purpose: backing off turns the bot away, so a retreat they cut
        // short would otherwise leave it cornered with its back to the target, unable to swing.
        if (desired == 5 && !FacingTarget)
        {
            desired = TargetOffsetX > 0f ? 6 : 4;
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
    /// Collects a spell if there's one going. Returns true when the bot is busy shopping and the
    /// caller should leave it alone this tick.
    ///
    /// Two different hits are needed here, and they are not interchangeable: the Gamba only responds
    /// to the owner's BASIC attack (ProcessPlayerBasicAttackCollision), while the lobby gate only
    /// breaks to a SPELL projectile. Swapping them silently does nothing at all.
    /// </summary>
    private bool TryShop()
    {
        FloppyPickup floppy = BestOwnFloppy();
        if (floppy != null)
        {
            CollectFloppy(floppy.transform.position, floppy.colliderRadius, owner.collidingWithFloppy);
            return true;
        }

        // Showdown's character disks. collidingWithFloppy is only ever set by spell disks, so ask the
        // disk itself whether this bot is the one standing on it. On a character disk the press also
        // opens CodeWeave (Idle only holds that back for spell disks), and the release still collects
        // -- the same thing that happens to a human picking one up.
        FloppyPickup_Character characterDisk = OwnCharacterDisk();
        if (characterDisk != null)
        {
            CollectFloppy(characterDisk.transform.position, characterDisk.colliderRadius,
                characterDisk.colliding && characterDisk.overlappingPlayer == owner);
            return true;
        }

        // No disks out: knock the Gamba to deal some. It re-arms on its own every 60 frames, up to
        // three times, so there is no need to track spins here.
        GambaMachine gamba = OwnGamba();
        if (gamba == null || !gamba.isActive)
        {
            return false;
        }

        // The swing only connects at the Gamba's own height. From the disk platform above it the
        // horizontal check below still reads "in range", and the bot whiffs over the top forever.
        // Get down to its level first; TravelToward plans the drop.
        Vector2 gambaPosition = gamba.transform.position;
        if (Mathf.Abs(gambaPosition.y - owner.position.Y.ToFloat()) > GambaHeightTolerance)
        {
            TravelToward(gambaPosition, GateCastDistance, GambaHeightTolerance);
            return true;
        }

        float offset = gamba.transform.position.x - owner.position.X.ToFloat();
        bool gambaIsRight = offset > 0f;

        if (owner.facingRight != gambaIsRight)
        {
            SetDirection(gambaIsRight ? 6 : 4);
            return true;
        }

        if (Mathf.Abs(offset) > GateCastDistance)
        {
            TravelTowardX(gamba.transform.position.x, GateCastDistance);
            return true;
        }

        Neutral();
        if (IsGrounded && framesSinceAttempt >= FramesBetweenAttempts && BeginBasicAttack())
        {
            framesSinceAttempt = 0;
        }

        return true;
    }

    /// <summary>
    /// Walks onto a floppy and taps Code to take it. The pickup radius is 18, so this has to stand
    /// much closer than the door does.
    /// </summary>
    private void CollectFloppy(Vector2 target, float pickupRadius, bool standingOnDisk)
    {
        Vector2 position = new Vector2(owner.position.X.ToFloat(), owner.position.Y.ToFloat());
        // standingOnDisk may describe a different disk (collidingWithFloppy is true on any of this
        // bot's spell disks). Only stop for the selected pickup.
        if (!standingOnDisk
            || (target - position).sqrMagnitude > pickupRadius * pickupRadius)
        {
            TravelToward(target, FloppyReachRadius, FloppyReachRadius);
            return;
        }

        StopNavigation();
        Neutral();
        if (framesSinceAttempt >= FramesBetweenAttempts && BeginFloppyPickup())
        {
            framesSinceAttempt = 0;
        }
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

        TravelToward(door.transform.position, DoorArrivalRadius, DoorArrivalRadius);
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
        // starting one mid-scramble is how a bot gets punished. How eagerly it comes back for
        // another go is a difficulty knob.
        if (!IsGrounded || framesSinceAttempt < Tuning.FramesBetweenAttempts)
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

        // A low tier reaches for whatever is ready instead of what the situation wants.
        SpellData spell = Tuning.PicksBestSpell ? ChooseSpell() : AnyReadySpell();
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
        float required = MinCastSpace + (length * SpacePerCodeStep);

        // Discipline is the clearest difficulty knob there is: a reckless tier starts long codes at
        // ranges it will be punished for, which is exactly what makes it beatable.
        required *= Tuning.CommitmentSpaceScale;

        // On the last stock a trade is a loss, so demand more room before committing to anything.
        if (IsOnLastStock())
        {
            required *= 1.6f;
        }

        return TargetDistanceX >= required;
    }

    /// <summary>
    /// Closes when the gap is wider than the band, backs off when it's tighter, holds otherwise.
    /// The dead band in the middle is what stops the bot vibrating on the spot.
    /// </summary>
    private int ChooseDirection()
    {
        int toward = TargetOffsetX > 0f ? 6 : 4;
        int away = TargetOffsetX > 0f ? 4 : 6;

        // Sit further out when a single mistake ends the match.
        float preferred = IsOnLastStock() ? PreferredDistance * 1.35f : PreferredDistance;

        if (TargetDistanceX > preferred + BandWidth)
        {
            return toward;
        }

        if (TargetDistanceX < preferred - BandWidth)
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
            if (!IsFalling)
            {
                return false;
            }

            // Vertical pursuit is handled by TravelToward. This is recovery after knockback
            // or an unplanned departure from the stage.
            return OverAVoid();
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

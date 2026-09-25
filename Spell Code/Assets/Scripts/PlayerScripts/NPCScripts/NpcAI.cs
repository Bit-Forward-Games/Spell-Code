using System;
using UnityEngine;

/// <summary>
/// Base class for everything that drives a PlayerController from code, whether that is a training
/// dummy in GameManager.playerNPCs or a bot holding a real slot in GameManager.players
/// </summary>
public abstract class NpcAI : MonoBehaviour
{
    [NonSerialized] public InputSnapshot npcInputSnapshot = new InputSnapshot(5,new ButtonState[]{ButtonState.None, ButtonState.None, ButtonState.None});
    [NonSerialized] public PlayerController owner;

    /// <summary>
    /// How many frames this behaviour's view of the world is allowed to go stale. 0 re-perceives
    /// every frame, which is superhuman; larger values are the honest way to model a slower
    /// opponent, because the bot acts on where things *were*.
    /// </summary>
    [NonSerialized] public int reactionFrames = 0;

    private int framesUntilPerception;

    // Intent for the current tick, reset before every NPCUpdate.
    private int intentDirection = 5;
    private bool intentCode;
    private bool intentJump;
    private bool intentUsed;

    // Previous tick's held levels, so the base can derive button edges the same way
    // InputPlayerBindings does for a real device.
    private bool previousCode;
    private bool previousJump;

    // Ticks since the last jump tap, so TapJump can enforce its refractory gap.
    private int framesSinceJumpTap = int.MaxValue / 2;

    // True while a jump is in progress and the button should stay down for height.
    private bool jumpHolding;

    private readonly BotPlatformNavigator platformNavigator = new BotPlatformNavigator();
    private BotPlatformNavigator.Step navigationStep;
    private StageDataSO navigationStage;
    private Vector2 navigationGoal;
    private bool hasNavigationStep;
    private bool navigationAirborne;
    private bool navigationClearedLedge;
    private int navigationFrames;
    private int navigationJumpsRemaining;

    // How far from a planned jump or drop takeoff the bot may stop and still re-check the move from
    // where it stands. Comfortably wider than the few pixels it overshoots by, but local to the plan.
    private const float TakeoffSlack = 12f;

    private bool droppingThroughPlatform;
    private StageDataSO dropStage;
    private float dropSurfaceY;
    private int dropFrames;

    protected bool IsNavigating => hasNavigationStep;

    // Horizontal progress tracking, for IsStuck.
    private float lastTrackedX;
    private int framesWithoutProgress;

    public abstract string BehaviorName { get; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(owner == null)
        {
            owner = gameObject.GetComponent<PlayerController>();
        }
    }

    public abstract void NPCUpdate();

    #region Perception

    /// <summary>The opponent this behaviour is acting against, or null when there isn't one.</summary>
    protected PlayerController Target { get; private set; }

    protected bool HasTarget => Target != null;

    /// <summary>Signed horizontal offset to the target. Positive means the target is to the right.</summary>
    protected float TargetOffsetX { get; private set; }

    /// <summary>Signed vertical offset to the target. Positive means the target is above.</summary>
    protected float TargetOffsetY { get; private set; }

    protected float TargetDistanceX => Mathf.Abs(TargetOffsetX);

    protected bool TargetIsGrounded { get; private set; }

    protected PlayerState TargetState { get; private set; }

    protected bool TargetFacingRight { get; private set; }

    /// <summary>
    /// False when a platform route to the target provably doesn't exist -- the other floor of a
    /// two-level arena like Dual Duel. Chasing someone there is a jump loop under the ceiling.
    /// </summary>
    protected bool TargetReachable { get; private set; }

    protected bool IsGrounded => owner != null && owner.isGrounded;

    protected PlayerState State => owner != null ? owner.state : PlayerState.Idle;

    /// <summary>True when the owner is already turned toward the target.</summary>
    protected bool FacingTarget => owner != null && HasTarget && owner.facingRight == (TargetOffsetX > 0f);

    /// <summary>
    /// Picks who to act against. Nearest living opponent by horizontal distance. Override for a
    /// behaviour that should care about something else.
    /// </summary>
    protected virtual PlayerController SelectTarget()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || owner == null)
        {
            return null;
        }

        PlayerController nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < gameManager.playerCount; i++)
        {
            PlayerController candidate = gameManager.players[i];
            if (candidate == null || candidate == owner || !candidate.isAlive || !candidate.isConnected)
            {
                continue;
            }

            float distance = Mathf.Abs(candidate.position.X.ToFloat() - owner.position.X.ToFloat());
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>
    /// True unless there is provably no platform route from where the owner stands to the candidate.
    /// Judged from the surface underfoot, so in mid-air it answers true rather than writing every
    /// opponent off.
    /// </summary>
    protected bool CanReach(PlayerController candidate)
    {
        if (owner == null || candidate == null)
        {
            return false;
        }

        StageDataSO stage = Stage;
        if (stage == null || !IsGrounded)
        {
            return true;
        }

        return platformNavigator.CanReach(stage,
            new Vector2(owner.position.X.ToFloat(), owner.position.Y.ToFloat()),
            new Vector2(candidate.position.X.ToFloat(), candidate.position.Y.ToFloat()),
            owner.playerWidth.ToFloat() * 0.5f, owner.playerHeight.ToFloat(),
            owner.jumpForce.ToFloat(), PlayerController.baseGravity, owner.runSpeed.ToFloat(),
            owner.maxJumpCount);
    }

    private void RefreshPerception()
    {
        Target = SelectTarget();
        if (Target == null || owner == null)
        {
            TargetOffsetX = 0f;
            TargetOffsetY = 0f;
            TargetIsGrounded = false;
            TargetState = PlayerState.Idle;
            TargetFacingRight = false;
            TargetReachable = false;
            return;
        }

        TargetOffsetX = Target.position.X.ToFloat() - owner.position.X.ToFloat();
        TargetOffsetY = Target.position.Y.ToFloat() - owner.position.Y.ToFloat();
        TargetIsGrounded = Target.isGrounded;
        TargetState = Target.state;
        TargetFacingRight = Target.facingRight;
        TargetReachable = CanReach(Target);
    }

    #endregion

    #region Terrain

    protected StageDataSO Stage =>
        GameManager.Instance != null ? GameManager.Instance.GetCurrentStageDataSO() : null;

    /// <summary>
    /// True when something walkable sits under worldX, no more than probeDepth below fromY. Reads
    /// the stage's solid and platform AABBs straight off StageDataSO -- the same arrays
    /// CheckStageDataSOCollision walks -- so there is no raycast and no physics dependency.
    /// </summary>
    protected bool HasGroundAt(float worldX, float fromY, float probeDepth = 96f)
    {
        StageDataSO stage = Stage;
        if (stage == null)
        {
            return false;
        }

        // A little headroom above the query point, so standing exactly on a surface still counts.
        float lowest = fromY - probeDepth;
        float highest = fromY + 8f;

        return HasSurfaceWithin(stage.solidCenter, stage.solidExtent, worldX, lowest, highest)
            || HasSurfaceWithin(stage.platformCenter, stage.platformExtent, worldX, lowest, highest);
    }

    private static bool HasSurfaceWithin(Vector2[] centers, Vector2[] extents, float worldX, float lowest, float highest)
    {
        if (centers == null || extents == null)
        {
            return false;
        }

        int count = Mathf.Min(centers.Length, extents.Length);
        for (int i = 0; i < count; i++)
        {
            // Extents are half-extents, matching how the collision pass reads them.
            if (worldX < centers[i].x - extents[i].x || worldX > centers[i].x + extents[i].x)
            {
                continue;
            }

            float top = centers[i].y + extents[i].y;
            if (top >= lowest && top <= highest)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when taking a step in this numpad direction keeps the owner on the stage: there is
    /// ground to arrive on, and the step stays inside the stage borders. Vertical and neutral
    /// directions are always safe, since they aren't a step.
    /// </summary>
    protected bool IsStepSafe(int numpadDirection, float lookAhead = 44f)
    {
        if (owner == null)
        {
            return false;
        }

        // 2, 5 and 8 are the middle column: no horizontal movement to vet.
        if (numpadDirection % 3 == 2)
        {
            return true;
        }

        float stepX = owner.position.X.ToFloat()
            + (numpadDirection % 3 == 0 ? lookAhead : -lookAhead);

        StageDataSO stage = Stage;
        if (stage != null && stage.borderMin != stage.borderMax
            && (stepX < stage.borderMin.x || stepX > stage.borderMax.x))
        {
            return false;
        }

        // The hazard being vetted is a fall, not an obstacle. A surface ABOVE the step is a step-up
        // or a wall: harmless to walk into, and refusing it would freeze the bot in front of every
        // raised ledge instead of letting it bump the wall and jump. So probe below, then again
        // from higher up to catch anything standing at or above foot level.
        float footY = owner.position.Y.ToFloat();
        return HasGroundAt(stepX, footY) || HasGroundAt(stepX, footY + 96f);
    }

    /// <summary>True when there is nothing to land on directly below the owner.</summary>
    protected bool OverAVoid(float probeDepth = 160f)
    {
        return owner != null
            && !HasGroundAt(owner.position.X.ToFloat(), owner.position.Y.ToFloat(), probeDepth);
    }

    protected bool IsFalling => owner != null && owner.vSpd.ToFloat() < 0f;

    protected bool CanJump => owner != null && owner.jumpCount > 0;

    /// <summary>
    /// True when the owner is pressed against something in this direction. The collision pass
    /// resolves along the smallest penetration axis, so even a shin-high bump blocks horizontally
    /// and sets this rather than letting the owner step up onto it.
    /// </summary>
    protected bool BlockedTowards(int numpadDirection)
    {
        if (owner == null)
        {
            return false;
        }

        if (numpadDirection % 3 == 0)
        {
            return owner.touchingRightWall;
        }

        if (numpadDirection % 3 == 1)
        {
            return owner.touchingLeftWall;
        }

        return false;
    }

    /// <summary>
    /// True when a solid stands within reach in this direction at body height -- a wall, as opposed
    /// to a drop. Read off the stage AABBs rather than touchingLeftWall/touchingRightWall: those only
    /// light up on a frame the owner is actively pushing into a wall, so a bot deciding whether to
    /// keep stepping toward one would flip-flop between yes and no.
    /// </summary>
    protected bool WallAhead(int numpadDirection, float reach = 24f)
    {
        StageDataSO stage = Stage;
        if (owner == null || stage == null || stage.solidCenter == null || stage.solidExtent == null
            || numpadDirection % 3 == 2)
        {
            return false;
        }

        float sign = numpadDirection % 3 == 0 ? 1f : -1f;
        float bodyEdge = owner.position.X.ToFloat() + sign * owner.playerWidth.ToFloat() * 0.5f;
        float nearX = Mathf.Min(bodyEdge, bodyEdge + sign * reach);
        float farX = Mathf.Max(bodyEdge, bodyEdge + sign * reach);
        float feet = owner.position.Y.ToFloat();
        float head = feet + owner.playerHeight.ToFloat();

        int count = Mathf.Min(stage.solidCenter.Length, stage.solidExtent.Length);
        for (int i = 0; i < count; i++)
        {
            Vector2 center = stage.solidCenter[i];
            Vector2 extent = stage.solidExtent[i];
            if (center.x + extent.x < nearX || center.x - extent.x > farX)
            {
                continue;
            }

            // Anything the body would run into: the floor under its feet doesn't count, a shin-high
            // bump does, because the collision pass blocks those sideways rather than stepping up.
            if (center.y + extent.y <= feet + 1f || center.y - extent.y >= head - 1f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// True when there is somewhere to land further along in this direction, past whatever the next
    /// step falls into. This is what separates a gap worth jumping from the edge of the world.
    /// </summary>
    protected bool HasLandingBeyond(int numpadDirection, float from = 44f, float to = 150f, float stride = 18f)
    {
        if (owner == null || numpadDirection % 3 == 2)
        {
            return false;
        }

        float sign = numpadDirection % 3 == 0 ? 1f : -1f;
        float footY = owner.position.Y.ToFloat();

        for (float distance = from; distance <= to; distance += stride)
        {
            float probeX = owner.position.X.ToFloat() + (sign * distance);
            if (HasGroundAt(probeX, footY) || HasGroundAt(probeX, footY + 96f))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the owner has been asking to move but hasn't actually gone anywhere. A catch-all
    /// for geometry the probes read as walkable and the collision pass disagrees about.
    /// </summary>
    protected bool IsStuck(int stuckFrames = 20) => framesWithoutProgress >= stuckFrames;

    /// <summary>
    /// Walks toward a world X, jumping over what can be jumped and refusing only a genuine drop.
    /// Use this rather than SetDirection whenever the bot is trying to *get somewhere*: plain
    /// ledge safety on its own turns every bump and gap into a permanent stop.
    /// </summary>
    protected void TravelTowardX(float worldX, float arriveWithin = 0f)
    {
        if (owner == null)
        {
            Neutral();
            return;
        }

        float offset = worldX - owner.position.X.ToFloat();
        if (Mathf.Abs(offset) <= arriveWithin)
        {
            Neutral();
            return;
        }

        int toward = offset > 0f ? 6 : 4;

        // Airborne, every step is fine -- that's how the bot crosses anything.
        bool stepSafe = !IsGrounded || IsStepSafe(toward);
        if (!stepSafe && !HasLandingBeyond(toward))
        {
            // Nothing to land on out there. This one really is the edge.
            Neutral();
            return;
        }

        SetDirection(toward);

        if (IsGrounded && CanJump && (BlockedTowards(toward) || !stepSafe || IsStuck()))
        {
            TapJump();
        }
    }

    /// <summary>
    /// Travels between walkable surfaces, preserving a chosen landing until the bot touches down.
    /// Both pickups and opponents use this, so horizontal alignment cannot prevent a climb/drop.
    /// </summary>
    protected void TravelToward(Vector2 target, float arriveWithin = 12f, float heightTolerance = 24f)
    {
        if (owner == null)
        {
            Neutral();
            return;
        }

        Vector2 position = new Vector2(owner.position.X.ToFloat(), owner.position.Y.ToFloat());
        StageDataSO stage = Stage;
        if (navigationStage != stage || State == PlayerState.Hitstun || State == PlayerState.Tech
            || (hasNavigationStep && ++navigationFrames > 180)
            || (hasNavigationStep && !navigationAirborne && (target - navigationGoal).sqrMagnitude > 4096f))
        {
            StopNavigation();
        }

        if (hasNavigationStep && !IsGrounded && !navigationAirborne)
        {
            navigationAirborne = true;
            navigationFrames = 0;
        }
        if (hasNavigationStep && navigationAirborne && IsGrounded)
        {
            StopNavigation();
        }

        if (!hasNavigationStep)
        {
            if (Mathf.Abs(target.x - position.x) <= arriveWithin
                && Mathf.Abs(target.y - position.y) <= heightTolerance)
            {
                SteerTowardX(target.x, arriveWithin);
                return;
            }

            // Plan from a supported surface. If knocked off a route, recover using the existing
            // air steering until a landing provides a new starting surface.
            if (!IsGrounded || !platformNavigator.TryGetNextStep(stage, position, target,
                owner.playerWidth.ToFloat() * 0.5f, owner.playerHeight.ToFloat(),
                owner.jumpForce.ToFloat(), PlayerController.baseGravity, owner.runSpeed.ToFloat(),
                owner.maxJumpCount, out navigationStep))
            {
                TravelTowardX(target.x, arriveWithin);
                if (target.y > position.y + heightTolerance || (IsFalling && OverAVoid()))
                {
                    ClimbTo(Mathf.Max(target.y, position.y + heightTolerance + 1f), heightTolerance);
                }
                return;
            }

            if (navigationStep.kind == BotPlatformNavigator.Kind.Walk)
            {
                SteerTowardX(navigationStep.landing.x, arriveWithin);
                // Some disks hover above their supporting floor rather than resting on it.
                if (Mathf.Abs(target.x - position.x) <= arriveWithin)
                {
                    ClimbTo(target.y, heightTolerance);
                }
                return;
            }

            navigationStage = stage;
            navigationGoal = target;
            navigationFrames = 0;
            navigationAirborne = false;
            navigationClearedLedge = false;
            navigationJumpsRemaining = navigationStep.jumpsRequired;
            hasNavigationStep = true;
        }

        if (!navigationAirborne)
        {
            float takeoffTolerance = navigationStep.kind == BotPlatformNavigator.Kind.Drop ? 5f : 1f;

            // From rest the smallest move the owner can make is ~5px (a unit of speed every two run
            // frames, then braking a unit every four), so it can overshoot a takeoff window from
            // either side forever -- that left a bot shuffling under its Shop disk and never
            // jumping, and on its disk platform over its Gamba and never dropping. Whenever it stops
            // near the takeoff, ask the planner whether the same jump or drop still lands from right
            // here, and take it from here if so.
            if ((navigationStep.kind == BotPlatformNavigator.Kind.Jump
                    || navigationStep.kind == BotPlatformNavigator.Kind.Drop)
                && Mathf.Abs(position.x - navigationStep.takeoff.x) > takeoffTolerance
                && Mathf.Abs(position.x - navigationStep.takeoff.x) <= TakeoffSlack
                && IsGrounded && Mathf.Abs(owner.hSpd.ToFloat()) < 0.1f
                && platformNavigator.TryTakeOffFrom(navigationStep, position.x, out BotPlatformNavigator.Step fromHere))
            {
                navigationStep = fromHere;
            }

            if (Mathf.Abs(position.x - navigationStep.takeoff.x) > takeoffTolerance)
            {
                // The planner has checked this approach. In particular, a planned fall MUST be
                // allowed past the edge; the generic ledge guard would forbid the entire route.
                SteerTowardX(navigationStep.takeoff.x, takeoffTolerance);
                return;
            }

            Neutral();
            if (navigationStep.kind == BotPlatformNavigator.Kind.Drop)
            {
                TryDropToward(navigationStep.landing.y, 0f);
            }
            else if (navigationStep.kind == BotPlatformNavigator.Kind.Jump
                && (State == PlayerState.Idle || State == PlayerState.Run) && CanJump
                && Mathf.Abs(owner.hSpd.ToFloat()) < 0.1f)
            {
                TapJump();
                if (intentJump)
                {
                    navigationJumpsRemaining--;
                }
            }
            return;
        }

        if (navigationStep.kind == BotPlatformNavigator.Kind.Fall
            && position.y >= navigationStep.takeoff.y - 2f)
        {
            // Keep moving off the lip through the zero-velocity airborne frame. Turning back
            // toward a lower target at once can put the body back on the source surface.
            SteerTowardX(navigationStep.takeoff.x, 0f);
            return;
        }

        float landingX = navigationStep.landing.x;
        navigationClearedLedge |= position.y >= navigationStep.landing.y + 2f;
        if (navigationStep.kind == BotPlatformNavigator.Kind.Jump
            && !navigationStep.destinationOneWay && !navigationClearedLedge)
        {
            // Rise beside a solid ledge, then move over its top. Moving inward any earlier
            // strikes its side/underside and wastes the jump.
            float clearance = owner.playerWidth.ToFloat() * 0.5f + 3f;
            if (navigationStep.takeoff.x < navigationStep.destinationLeft)
            {
                landingX = Mathf.Min(landingX, navigationStep.destinationLeft - clearance);
            }
            else if (navigationStep.takeoff.x > navigationStep.destinationRight)
            {
                landingX = Mathf.Max(landingX, navigationStep.destinationRight + clearance);
            }
        }
        SteerTowardX(landingX, 4f);

        if (navigationJumpsRemaining > 0 && IsFalling && CanJump)
        {
            TapJump();
            if (intentJump)
            {
                navigationJumpsRemaining--;
            }
        }
    }

    protected void StopNavigation()
    {
        hasNavigationStep = false;
        navigationAirborne = false;
        navigationClearedLedge = false;
        navigationFrames = 0;
        navigationJumpsRemaining = 0;
    }

    private void SteerTowardX(float worldX, float tolerance)
    {
        float offset = worldX - owner.position.X.ToFloat();
        float speed = owner.hSpd.ToFloat();
        // Neutral braking changes speed by one every three frames in the air, four on the
        // ground. Account for that drift when lining up a takeoff or a narrow landing.
        float brakingDistance = Mathf.Abs(speed) * (Mathf.Abs(speed) + 1f) * (IsGrounded ? 2f : 1.5f);
        if (Mathf.Abs(offset) <= tolerance
            || (speed * offset > 0f && Mathf.Abs(offset) <= brakingDistance))
        {
            Neutral();
        }
        else
        {
            SetDirection(offset > 0f ? 6 : 4);
        }
    }

    /// <summary>
    /// This bot's own lobby gate, or null when there isn't one in the current scene. A gate only
    /// ever breaks to a projectile fired by its own owner, so there is no point looking at anyone
    /// else's.
    /// </summary>
    protected SpellCode_Gate OwnGate()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || owner == null || gameManager.gates == null)
        {
            return null;
        }

        for (int i = 0; i < gameManager.gates.Length; i++)
        {
            SpellCode_Gate gate = gameManager.gates[i];
            if (gate != null && gate.ownerPID == owner.pID)
            {
                return gate;
            }
        }

        return null;
    }

    #endregion

    #region Shopping

    private const int FloppyScanInterval = 15;

    private FloppyPickup cachedFloppy;
    private int framesUntilFloppyScan;

    /// <summary>True while the owner can still take another spell.</summary>
    protected bool HasSpellRoom => owner != null && owner.spellList != null && owner.spellList.Count < 6;

    /// <summary>This bot's own Gamba machine, or null when there isn't one in this scene.</summary>
    protected GambaMachine OwnGamba()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager != null && owner != null ? gameManager.GetGambaForPID(owner.pID) : null;
    }

    /// <summary>
    /// The floppy this bot should take, or null. Floppies are per-player, so anyone else's are
    /// invisible here. The Shop puts several out at once, so this picks on what the spell is worth
    /// to this kit rather than which disk happens to be closest.
    ///
    /// Scanned on an interval rather than every tick, and re-checked for null in between because a
    /// disk vanishes the moment it's collected.
    /// </summary>
    protected FloppyPickup BestOwnFloppy()
    {
        if (owner == null)
        {
            return null;
        }

        if (framesUntilFloppyScan > 0)
        {
            framesUntilFloppyScan--;
            return cachedFloppy != null ? cachedFloppy : null;
        }

        framesUntilFloppyScan = FloppyScanInterval;
        cachedFloppy = null;

        FloppyPickup[] floppies = FindObjectsByType<FloppyPickup>(FindObjectsSortMode.None);
        float bestScore = float.MinValue;

        for (int i = 0; i < floppies.Length; i++)
        {
            FloppyPickup floppy = floppies[i];
            if (floppy == null || floppy.ownerPID != owner.pID)
            {
                continue;
            }

            // Distance still breaks ties, just quietly: a disk worth more is worth a longer walk.
            float distance = Mathf.Abs(floppy.transform.position.x - owner.position.X.ToFloat());
            float score = ScoreFloppy(floppy) - (distance * 0.002f);

            if (score > bestScore)
            {
                bestScore = score;
                cachedFloppy = floppy;
            }
        }

        return cachedFloppy;
    }

    private FloppyPickup_Character cachedCharacterDisk;
    private int framesUntilCharacterDiskScan;

    /// <summary>
    /// The nearer of this bot's Showdown character disks, or null. Showdown deals whole characters on
    /// FloppyPickup_Character, a different component from FloppyPickup under the same tag, so
    /// BestOwnFloppy never sees them -- and a Showdown bot without this knocks its Gamba forever and
    /// never holds a spell to open its gate with. Same interval scan and null re-check as
    /// BestOwnFloppy; the two characters on offer are whole kits, so there is nothing to score.
    /// </summary>
    protected FloppyPickup_Character OwnCharacterDisk()
    {
        if (owner == null)
        {
            return null;
        }

        if (framesUntilCharacterDiskScan > 0)
        {
            framesUntilCharacterDiskScan--;
            return cachedCharacterDisk != null ? cachedCharacterDisk : null;
        }

        framesUntilCharacterDiskScan = FloppyScanInterval;
        cachedCharacterDisk = null;

        FloppyPickup_Character[] disks = FindObjectsByType<FloppyPickup_Character>(FindObjectsSortMode.None);
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < disks.Length; i++)
        {
            FloppyPickup_Character disk = disks[i];
            if (disk == null || disk.ownerPID != owner.pID)
            {
                continue;
            }

            float distance = Mathf.Abs(disk.transform.position.x - owner.position.X.ToFloat());
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                cachedCharacterDisk = disk;
            }
        }

        return cachedCharacterDisk;
    }

    /// <summary>
    /// How much a disk is worth to this bot. Favours range bands the kit is thin on, so a bot ends
    /// up with an answer at more than one distance instead of four versions of the same poke.
    /// </summary>
    private float ScoreFloppy(FloppyPickup floppy)
    {
        SpellDictionary dictionary = SpellDictionary.Instance;
        if (dictionary == null
            || floppy == null
            || string.IsNullOrEmpty(floppy.diskName)
            || !dictionary.spellDict.TryGetValue(floppy.diskName, out SpellData spell)
            || spell == null)
        {
            // An unrecognised disk is still better than walking away empty-handed.
            return 0.5f;
        }

        SpellTactics.Profile profile = SpellTactics.For(spell);

        int alreadyInBand = 0;
        if (owner.spellList != null)
        {
            for (int i = 0; i < owner.spellList.Count; i++)
            {
                SpellData held = owner.spellList[i];
                if (held != null
                    && held.spellType == SpellType.Active
                    && SpellTactics.For(held).Range == profile.Range)
                {
                    alreadyInBand++;
                }
            }
        }

        float score = 1f / (1f + alreadyInBand);

        switch (profile.Role)
        {
            case SpellRole.Attack:
                score += 0.35f;
                break;
            case SpellRole.Utility:
                // Nothing triggers these yet, so they're close to dead weight in a bot's hands.
                score -= 0.2f;
                break;
        }

        return score;
    }

    #endregion

    #region Casting

    private uint castCode;
    private int castStepIndex;
    private int castStepFrames;
    private bool castOnNeutralPhase;
    private bool casting;

    protected bool IsCasting => casting;

    /// <summary>
    /// Frames to hold each half of a code step. One is enough, since the sim reads input every
    /// frame, but two is cheap insurance and reads less like a machine.
    /// </summary>
    protected int castStepHoldFrames = 2;

    // Hold length for the cast actually running, so one action can ask for a shorter press than the
    // default without permanently retuning code entry.
    private int activeCastHoldFrames = 2;

    /// <summary>
    /// Starts entering a spell's code. Generic across every spell in the game with no per-spell
    /// authoring, because spellInput already carries the whole sequence: bits 0-3 hold the length,
    /// and each pair from bit 8 up is one direction.
    /// </summary>
    /// <summary>How this bot's difficulty tier wants it to play. See BotTuning.</summary>
    protected BotTuning Tuning =>
        BotTuning.For(owner != null ? owner.botDifficulty : BotDifficulty.Medium);

    protected bool BeginCast(SpellData spell)
    {
        if (spell == null || spell.spellType != SpellType.Active || spell.cooldownCounter > 0)
        {
            return false;
        }

        uint code = spell.spellInput;

        // Punk rewrites code entry completely: every direction writes to slot 0 and the length
        // stops at one, so entering the real sequence would collapse into whichever direction
        // happened to land last -- the wrong spell, or none. Under Punk a spell is cast by the one
        // direction matching its place in the list, and only the first four places are reachable.
        if (owner != null && owner.vibeCoding)
        {
            int slot = owner.spellList != null ? owner.spellList.IndexOf(spell) : -1;
            if (slot < 0 || slot > 3)
            {
                return false;
            }

            code = PunkCodeForSlot(slot);
        }

        return BeginCast(code);
    }

    /// <summary>
    /// The single-direction code Punk uses for a spell-list slot, matching the shortcuts
    /// CheckSpellCodeInput matches against: up, right, down, left for slots 0-3.
    /// </summary>
    private static uint PunkCodeForSlot(int slot)
    {
        uint directionBits;
        switch (slot)
        {
            case 0: directionBits = 0b11; break;  // up
            case 1: directionBits = 0b01; break;  // right
            case 2: directionBits = 0b00; break;  // down
            default: directionBits = 0b10; break; // left
        }

        // One direction at slot 0, length 1.
        return (directionBits << 8) | 1u;
    }

    /// <summary>A bare Code press and release, with no directions entered.</summary>
    protected bool BeginBasicAttack() => BeginCast(0u);

    /// <summary>
    /// Taps Code to take the floppy the owner is stood on. Deliberately allowed on a floppy, where
    /// BeginCast normally refuses: here the press being swallowed as a pickup is the point.
    ///
    /// Held for a single frame rather than the usual two. The disk collects on the RELEASE edge and
    /// accepts any hold under 30 frames, including none at all, so the shortest possible press
    /// gives the best chance of the release landing while the bot is still inside the disk's small
    /// pickup radius, which matters when it has to jump to reach one.
    /// </summary>
    protected bool BeginFloppyPickup() => BeginCast(0u, allowOnFloppy: true, holdFrames: 1);

    protected bool BeginCast(uint spellInput, bool allowOnFloppy = false, int holdFrames = 0)
    {
        if (owner == null || casting)
        {
            return false;
        }

        // Pressing Code while stood on a floppy picks the floppy up instead of opening code entry,
        // so the press would be swallowed and the whole sequence would go in as movement. The one
        // caller that *wants* that swallowing opts in.
        if (owner.collidingWithFloppy && !allowOnFloppy)
        {
            return false;
        }

        castCode = spellInput;
        castStepIndex = 0;
        castStepFrames = 0;
        castOnNeutralPhase = true;
        activeCastHoldFrames = holdFrames > 0 ? holdFrames : castStepHoldFrames;
        casting = true;
        return true;
    }

    protected void CancelCast()
    {
        casting = false;
        castCode = 0;
        castStepIndex = 0;
        castStepFrames = 0;
        castOnNeutralPhase = true;
    }

    /// <summary>
    /// Emits one tick of an in-progress cast, returning false when there is nothing to emit.
    ///
    /// The sequence alternates neutral and direction. Neutral is not padding: a direction is only
    /// recorded while the code is "primed", and neutral is what sets that bit. Alternating is also
    /// what lets the same direction appear twice in a row, since a held direction matches the last
    /// entry in the queue and is ignored.
    /// </summary>
    private bool AdvanceCast()
    {
        if (!casting || owner == null)
        {
            return false;
        }

        // Getting hit ends code entry, so drop the plan rather than spending frames finishing a
        // code the owner is no longer in a state to enter.
        if (owner.state == PlayerState.Hitstun || owner.state == PlayerState.Tech)
        {
            CancelCast();
            return false;
        }

        HoldCode(true);

        int length = (int)(castCode & 0xF);
        if (castStepIndex >= length)
        {
            // Whole sequence is in. Hold a moment so the last direction settles, then release --
            // release is what actually casts.
            if (castStepFrames < activeCastHoldFrames)
            {
                castStepFrames++;
                Neutral();
                return true;
            }

            HoldCode(false);
            casting = false;
            return true;
        }

        if (castOnNeutralPhase)
        {
            Neutral();
        }
        else
        {
            SetDirection(CodeStepToNumpad(castCode, castStepIndex));
        }

        castStepFrames++;
        if (castStepFrames >= activeCastHoldFrames)
        {
            castStepFrames = 0;
            if (!castOnNeutralPhase)
            {
                castStepIndex++;
            }

            castOnNeutralPhase = !castOnNeutralPhase;
        }

        return true;
    }

    /// <summary>
    /// Unpacks one step of a code into the numpad direction that produces it. Assumes absolute
    /// directions: bots are set to relativeInputs = false at spawn, which is exactly so this
    /// mapping doesn't have to track the owner's facing mid-code.
    /// </summary>
    private static int CodeStepToNumpad(uint code, int stepIndex)
    {
        byte bits = (byte)((code >> (8 + (stepIndex * 2))) & 0b11);
        switch (bits)
        {
            case 0b00: return 2;
            case 0b01: return 6;
            case 0b10: return 4;
            default: return 8;
        }
    }

    /// <summary>
    /// Any ready active spell, for when the situation doesn't call for a judgement -- shooting an
    /// obstacle, say, where the only thing that matters is that a spell projectile comes out.
    /// canCast, when given, skips spells the caller can't afford right now.
    /// </summary>
    protected SpellData AnyReadySpell(Predicate<SpellData> canCast = null)
    {
        if (owner == null || owner.spellList == null)
        {
            return null;
        }

        for (int i = 0; i < owner.spellList.Count; i++)
        {
            SpellData spell = owner.spellList[i];
            if (spell != null
                && spell.spellType == SpellType.Active
                && spell.cooldownCounter <= 0
                && PlayerController.GetSpellInputLength(spell) > 0
                && (canCast == null || canCast(spell)))
            {
                return spell;
            }
        }

        return null;
    }

    /// <summary>
    /// Picks the best ready active spell for the situation, or null when none of them fit it.
    /// Every ready spell is scored on how well its range band matches the actual gap, then adjusted
    /// for what the spell is for and whether the target is off the ground. canCast, when given, is
    /// applied BEFORE scoring: filtering only the winner meant a best pick the bot couldn't afford
    /// cast nothing at all, even with a cheaper spell ready that fit.
    /// </summary>
    protected SpellData ChooseSpell(Predicate<SpellData> canCast = null)
    {
        if (owner == null || owner.spellList == null || !HasTarget)
        {
            return null;
        }

        bool targetAirborne = !TargetIsGrounded && TargetOffsetY > 32f;

        SpellData best = null;
        float bestScore = 0f;

        for (int i = 0; i < owner.spellList.Count; i++)
        {
            SpellData spell = owner.spellList[i];
            if (spell == null || spell.spellType != SpellType.Active || spell.cooldownCounter > 0)
            {
                continue;
            }

            if (PlayerController.GetSpellInputLength(spell) <= 0 || (canCast != null && !canCast(spell)))
            {
                continue;
            }

            float score = ScoreSpell(spell, TargetDistanceX, targetAirborne);
            if (score > bestScore)
            {
                bestScore = score;
                best = spell;
            }
        }

        return best;
    }

    private static float ScoreSpell(SpellData spell, float distance, bool targetAirborne)
    {
        SpellTactics.Profile profile = SpellTactics.For(spell);

        // A buff isn't aimed at anyone, so its range band says nothing about when to cast it --
        // scoring it by one zeroed every Enhance, since they're tagged Short (in band only inside
        // 90) while this rule wants them from 115 out. What a buff costs is recovery with no damage,
        // so it needs real space; given that, it scores a flat middling value that loses to any
        // attack sitting well in its band and wins when nothing is.
        if (profile.Role == SpellRole.Enhance)
        {
            if (distance < SpellTactics.IdealDistance(SpellRange.Medium))
            {
                return 0f;
            }

            return Mathf.Max(0.5f - PlayerController.GetSpellInputLength(spell) * 0.03f, 0f);
        }

        float ideal = SpellTactics.IdealDistance(profile.Range);
        float tolerance = SpellTactics.BandTolerance(profile.Range);
        float missBy = Mathf.Abs(distance - ideal);

        // Outside its band entirely: wrong tool, whatever else it has going for it.
        if (missBy > tolerance)
        {
            return 0f;
        }

        // 1 dead centre, tapering to 0 at the edge of the band.
        float score = 1f - (missBy / tolerance);

        switch (profile.Role)
        {
            case SpellRole.Zone:
                // Worth laying while they are still on their way in, not once they have arrived.
                if (distance < ideal * 0.6f)
                {
                    return 0f;
                }
                score *= 0.8f;
                break;

            case SpellRole.Utility:
                // Defensive and positional: wanted in reaction to something, and nothing yet asks
                // for one. Never opened with rather than thrown out at random.
                return 0f;
        }

        if (targetAirborne)
        {
            score += profile.HitsAbove ? 0.6f : -0.5f;
        }

        // Break ties toward shorter codes: fewer frames to enter, less recovery to sit through.
        score -= PlayerController.GetSpellInputLength(spell) * 0.03f;

        return Mathf.Max(score, 0f);
    }

    #endregion

    #region Intent

    /// <summary>Stand still: neutral direction, no buttons.</summary>
    protected void Neutral() => SetDirection(5);

    /// <summary>
    /// Sets the numpad direction for this tick (5 is neutral). The owner's state machine derives
    /// both facing and acceleration from this, so simply holding 4 or 6 turns and then runs.
    /// </summary>
    protected void SetDirection(int numpadDirection)
    {
        intentUsed = true;
        intentDirection = Mathf.Clamp(numpadDirection, 1, 9);
    }

    protected void MoveLeft() => SetDirection(4);

    protected void MoveRight() => SetDirection(6);

    /// <summary>
    /// Walks toward the target, stopping inside the deadzone so the bot doesn't jitter left and
    /// right on top of it. Neutral when there's nobody to walk toward.
    /// </summary>
    protected void MoveTowardTarget(float deadzone = 24f)
    {
        if (!HasTarget)
        {
            Neutral();
            return;
        }

        if (TargetDistanceX <= deadzone)
        {
            Neutral();
            return;
        }

        SetDirection(TargetOffsetX > 0f ? 6 : 4);
    }

    protected void MoveAwayFromTarget()
    {
        if (!HasTarget)
        {
            Neutral();
            return;
        }

        SetDirection(TargetOffsetX > 0f ? 4 : 6);
    }

    /// <summary>Holds or releases jump. Pass a level, not an edge -- the base derives the edge.</summary>
    protected void HoldJump(bool held = true)
    {
        intentUsed = true;
        intentJump = held;
    }

    /// <summary>Holds or releases the code button. Pass a level; the base derives the edge.</summary>
    protected void HoldCode(bool held = true)
    {
        intentUsed = true;
        intentCode = held;
    }

    /// <summary>
    /// Asks for a single jump. Use this rather than HoldJump for ordinary jumping: the owner only
    /// jumps on the Pressed edge, so a behaviour that simply holds the button jumps once and then
    /// never again, even after landing. This keeps the button released between taps and enforces a
    /// refractory gap so a standing condition can't burn every air jump in three frames.
    /// HoldJump is still the right call for shaping one jump's height, since releasing mid-rise
    /// cuts it short.
    /// </summary>
    protected void TapJump(int minFramesBetweenTaps = 6)
    {
        intentUsed = true;

        // Only starts a jump. Holding it for height is ApplyJumpHold's job, because by the time the
        // owner is rising no behaviour is asking to jump any more.
        if (jumpHolding || framesSinceJumpTap < minFramesBetweenTaps)
        {
            return;
        }

        intentJump = true;
        jumpHolding = true;
    }

    /// <summary>
    /// Keeps the jump button down for as long as the owner is rising, then releases at the apex so
    /// the next press still has an edge to land on.
    ///
    /// This runs from Tick rather than from TapJump because behaviours stop asking to jump the
    /// moment they leave the ground -- they gate on IsGrounded or IsFalling, and mid-ascent neither
    /// holds. PlayerUpdate re-applies DOUBLE gravity on every rising frame where jump is not held,
    /// so a button released at the start of the climb produces the minimum hop, every time.
    /// </summary>
    private void ApplyJumpHold()
    {
        if (!jumpHolding || intentJump)
        {
            // TapJump runs before PlayerUpdate applies the impulse. Its launch tick still has
            // zero vertical speed, so do not mistake a fresh press for the end of the jump.
            return;
        }

        if (owner != null && owner.vSpd.ToFloat() > 0f)
        {
            intentJump = true;
            intentUsed = true;
            return;
        }

        jumpHolding = false;
    }

    /// <summary>
    /// Falls through the one-way platform the owner is stood on, when the thing it wants is below.
    /// Down plus jump is the drop input, and this is gated on onPlatform because the same input on
    /// solid ground is a slide instead.
    /// </summary>
    protected bool TryDropToward(float targetWorldY, float threshold = 48f)
    {
        if (owner == null || !IsGrounded || !owner.onPlatform
            || owner.position.Y.ToFloat() - targetWorldY <= threshold)
        {
            return false;
        }

        // Run interprets a down+jump press as a slide. Settle to Idle, and release any previous
        // jump, before issuing the drop. A held jump must not leak out of ApplyJumpHold either.
        jumpHolding = false;
        Neutral();
        HoldJump(false);
        if (State != PlayerState.Idle || previousJump || Mathf.Abs(owner.hSpd.ToFloat()) > 0.1f)
        {
            return true;
        }

        droppingThroughPlatform = true;
        dropStage = Stage;
        dropSurfaceY = owner.position.Y.ToFloat();
        dropFrames = 0;
        return ContinuePlatformDrop();
    }

    private bool ContinuePlatformDrop()
    {
        if (!droppingThroughPlatform)
        {
            return false;
        }

        if (owner == null || Stage != dropStage || ++dropFrames > 24
            || owner.position.Y.ToFloat() < dropSurfaceY - 2f
            || State == PlayerState.Hitstun || State == PlayerState.Tech)
        {
            droppingThroughPlatform = false;
            return false;
        }

        // Ignoring a platform first clears grounded while vertical speed is still zero. Hold
        // through that frame and the next until feet are below the top, or collision re-lands us.
        jumpHolding = false;
        SetDirection(2);
        HoldJump(true);
        return true;
    }

    /// <summary>
    /// Jumps to reach something overhead, including the second jump that finishes a climb the first
    /// one fell short of. Safe to call every tick while the goal is above: TapJump owns both the
    /// hold-for-height and the gap between jumps.
    /// </summary>
    protected void ClimbTo(float targetWorldY, float threshold)
    {
        if (owner == null || !CanJump)
        {
            return;
        }

        if (targetWorldY - owner.position.Y.ToFloat() <= threshold)
        {
            return;
        }

        // From the ground, or while falling short partway up -- either way another jump is the move.
        if (IsGrounded || IsFalling)
        {
            TapJump();
        }
    }

    #endregion

    /// <summary>
    /// One simulation tick. Called from PlayerController.GetInputs, which packs the resulting
    /// snapshot into the same ulong a gamepad would produce.
    /// </summary>
    public void Tick()
    {
        if (owner == null)
        {
            owner = gameObject.GetComponent<PlayerController>();
        }

        if (framesUntilPerception <= 0)
        {
            RefreshPerception();
            framesUntilPerception = reactionFrames;
        }
        else
        {
            framesUntilPerception--;
        }

        intentUsed = false;
        intentDirection = 5;
        intentCode = false;
        intentJump = false;

        // A cast in progress owns the inputs outright. Running the behaviour alongside it would let
        // a movement direction overwrite a code step and silently corrupt the sequence into either
        // the wrong spell or none at all.
        if (!AdvanceCast())
        {
            if (!ContinuePlatformDrop())
            {
                NPCUpdate();
            }

            // If the behaviour started a cast just now, emit its opening frame here. Without this
            // the tick that begins a cast sets no intent at all, and the base would leave last
            // tick's snapshot standing -- a stale frame of movement right as the code opens.
            AdvanceCast();
        }

        ApplyJumpHold();

        framesSinceJumpTap = intentJump ? 0 : Mathf.Min(framesSinceJumpTap + 1, int.MaxValue / 2);
        UpdateStuckTracking();

        // A behaviour that never touched the intent API wrote npcInputSnapshot itself, the way the
        // training dummies always have. Leave their snapshot exactly as they left it.
        if (!intentUsed)
        {
            return;
        }

        npcInputSnapshot.Direction = intentDirection;
        npcInputSnapshot.ButtonStates[0] = ResolveEdge(previousCode, intentCode);
        npcInputSnapshot.ButtonStates[1] = ResolveEdge(previousJump, intentJump);
        // Index 2 is Pause. A bot must never open the pause menu; PlayerUpdate also refuses it, but
        // there is no reason to emit it in the first place.
        npcInputSnapshot.ButtonStates[2] = ButtonState.None;

        previousCode = intentCode;
        previousJump = intentJump;
    }

    /// <summary>
    /// Counts ticks where the bot asked to move and the owner didn't actually shift. Position is a
    /// frame behind here -- PlayerUpdate runs after GetInputs -- which is fine for spotting a stall.
    ///
    /// Only a grounded Run counts. Stuck means shoving against something, and that is the only state
    /// the check is for; while the owner can't act on input at all -- falling in from a spawn, or
    /// the whole screen-transition cover, which drops every slot's input -- asking without moving
    /// is expected. Counting those frames is what made a bot "unstick" with a jump on its very first
    /// step of a rematch lobby, straight up onto the disk platform over its own Gamba.
    /// </summary>
    private void UpdateStuckTracking()
    {
        if (owner == null)
        {
            return;
        }

        float currentX = owner.position.X.ToFloat();
        bool askedToMove = intentUsed && intentDirection % 3 != 2
            && owner.isGrounded && owner.state == PlayerState.Run;

        if (askedToMove && Mathf.Abs(currentX - lastTrackedX) < 0.35f)
        {
            framesWithoutProgress++;
        }
        else
        {
            framesWithoutProgress = 0;
        }

        lastTrackedX = currentX;
    }

    private static ButtonState ResolveEdge(bool previous, bool current)
    {
        if (!previous && !current)
        {
            return ButtonState.None;
        }

        if (current && !previous)
        {
            return ButtonState.Pressed;
        }

        if (current && previous)
        {
            return ButtonState.Held;
        }

        return ButtonState.Released;
    }
}

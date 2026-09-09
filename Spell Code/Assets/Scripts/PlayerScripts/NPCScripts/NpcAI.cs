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
            return;
        }

        TargetOffsetX = Target.position.X.ToFloat() - owner.position.X.ToFloat();
        TargetOffsetY = Target.position.Y.ToFloat() - owner.position.Y.ToFloat();
        TargetIsGrounded = Target.isGrounded;
        TargetState = Target.state;
        TargetFacingRight = Target.facingRight;
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

    /// <summary>
    /// Starts entering a spell's code. Generic across every spell in the game with no per-spell
    /// authoring, because spellInput already carries the whole sequence: bits 0-3 hold the length,
    /// and each pair from bit 8 up is one direction.
    /// </summary>
    protected bool BeginCast(SpellData spell)
    {
        if (spell == null || spell.spellType != SpellType.Active || spell.cooldownCounter > 0)
        {
            return false;
        }

        return BeginCast(spell.spellInput);
    }

    /// <summary>A bare Code press and release, with no directions entered.</summary>
    protected bool BeginBasicAttack() => BeginCast(0u);

    protected bool BeginCast(uint spellInput)
    {
        if (owner == null || casting)
        {
            return false;
        }

        // Pressing Code while stood on a floppy picks the floppy up instead of opening code entry,
        // so the press would be swallowed and the whole sequence would go in as movement.
        if (owner.collidingWithFloppy)
        {
            return false;
        }

        castCode = spellInput;
        castStepIndex = 0;
        castStepFrames = 0;
        castOnNeutralPhase = true;
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
            if (castStepFrames < castStepHoldFrames)
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
        if (castStepFrames >= castStepHoldFrames)
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
    /// The shortest ready active spell, or null. Shortest first because length is the commitment:
    /// it costs more frames to enter and CodeRelease recovery grows with it.
    /// </summary>
    protected SpellData ShortestReadySpell()
    {
        if (owner == null || owner.spellList == null)
        {
            return null;
        }

        SpellData shortest = null;
        int shortestLength = int.MaxValue;

        for (int i = 0; i < owner.spellList.Count; i++)
        {
            SpellData spell = owner.spellList[i];
            if (spell == null || spell.spellType != SpellType.Active || spell.cooldownCounter > 0)
            {
                continue;
            }

            int length = PlayerController.GetSpellInputLength(spell);
            if (length > 0 && length < shortestLength)
            {
                shortestLength = length;
                shortest = spell;
            }
        }

        return shortest;
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
    protected void TapJump(int minFramesBetweenTaps = 12)
    {
        intentUsed = true;
        if (framesSinceJumpTap < minFramesBetweenTaps)
        {
            return;
        }

        intentJump = true;
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
            NPCUpdate();

            // If the behaviour started a cast just now, emit its opening frame here. Without this
            // the tick that begins a cast sets no intent at all, and the base would leave last
            // tick's snapshot standing -- a stale frame of movement right as the code opens.
            AdvanceCast();
        }

        framesSinceJumpTap = intentJump ? 0 : Mathf.Min(framesSinceJumpTap + 1, int.MaxValue / 2);

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

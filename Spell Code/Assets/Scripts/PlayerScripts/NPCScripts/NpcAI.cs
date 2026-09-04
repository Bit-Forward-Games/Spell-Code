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
    /// opponent, because the bot acts on where things *were*. Phase 9 maps difficulty onto this.
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

        NPCUpdate();

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

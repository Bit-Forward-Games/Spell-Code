using UnityEngine;

/// <summary>
/// The first behaviour that actually plays: walk at the nearest living opponent and turn to face it
///
/// Facing comes free: the owner's Idle and Run states both turn toward the held direction before
/// accelerating, so holding 4 or 6 is the entire instruction.
/// </summary>
public class ChaseAI : NpcAI
{
    public override string BehaviorName => "Chase";

    public override void NPCUpdate()
    {
        MoveTowardTarget();
    }
}

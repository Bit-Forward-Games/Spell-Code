using UnityEngine;

public class HoldCodeButtonAI : NpcAI
{

    public override void NPCUpdate()
    {
        if(owner==null)return;
        if(owner.state == PlayerState.Idle)
        {
            npcInputSnapshot.ButtonStates[0] = ButtonState.Pressed;
        }
        else
        {
            npcInputSnapshot.ButtonStates[0] = ButtonState.Held;
        }
    }
}

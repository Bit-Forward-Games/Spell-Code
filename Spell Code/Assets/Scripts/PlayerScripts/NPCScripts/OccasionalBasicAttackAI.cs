using UnityEngine;

public class OccasionalBasicAttackAI : NpcAI
{


    public override void NPCUpdate()
    {
        if(owner==null)return;
        if(owner.state == PlayerState.Idle && owner.logicFrame == 60)
        {
            npcInputSnapshot.ButtonStates[0] = ButtonState.Pressed;
        }
        else
        {
            npcInputSnapshot.SetNull();
        }
    }
}

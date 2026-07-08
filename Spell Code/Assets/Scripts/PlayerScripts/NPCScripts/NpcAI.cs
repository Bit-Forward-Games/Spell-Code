using System;
using UnityEngine;

public abstract class NpcAI : MonoBehaviour
{
    [NonSerialized] public InputSnapshot npcInputSnapshot = new InputSnapshot(5,new ButtonState[]{ButtonState.None, ButtonState.None, ButtonState.None});
    [NonSerialized] public PlayerController owner;
    

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
}

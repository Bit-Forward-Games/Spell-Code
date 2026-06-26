using UnityEngine;

public abstract class NpcAI : MonoBehaviour
{
    public InputSnapshot npcInputSnapshot = new InputSnapshot(5,new ButtonState[]{ButtonState.None, ButtonState.None, ButtonState.None});
    public PlayerController owner;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(owner == null)
        {
            owner = gameObject.GetComponent<PlayerController>();
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    public abstract void NPCUpdate();
}

using UnityEngine;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
public class GO_Door : MonoBehaviour
{
    Animator animator;
    bool isOpen = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public bool CheckAllPlayersReady()
    {
        //first check if the door is open
        if (!isOpen)
        {
            return false;
        }
        PlayerController player;
        // check if all active players are within a certain range of the door
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            player = GameManager.Instance.players[i];
            // Convert transform.position.x (float) to Fixed32 before subtraction
            Fixed doorPosX = Fixed.FromFloat(this.transform.position.x);
            Fixed distanceX = doorPosX - player.position.X;

            Fixed absDistanceX = Fixed.Abs(distanceX);

            Fixed playerHalfWidth = player.playerWidth / Fixed.FromInt(2);

            if (absDistanceX > playerHalfWidth)
            {
                return false;
            }
        }

        //all players are in range and the door is open, then return true
        return true;
    }

    public bool CheckOpenDoor()
    {
        if(GameManager.Instance.playerCount > 1)
        {
             isOpen = true;

        }
        else
        {
            isOpen = false;
        }

        if(animator == null)
        {
            return isOpen;
        }
        if (isOpen != animator.GetBool("open"))
        {
            animator.SetBool("open", isOpen);
        }
        return isOpen;
    }

}

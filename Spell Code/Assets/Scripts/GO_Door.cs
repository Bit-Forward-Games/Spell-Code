using BestoNet.Types;
using UnityEngine;
using UnityEngine.UIElements;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
public class GO_Door : MonoBehaviour
{
    Animator animator;
    bool isOpen = false;
    float colliderRadius = 32;
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

            FixedVec2 doorPos = FixedVec2.FromFloat(transform.position.x, transform.position.y);
            // Compute squared distance (avoid square root):
            Fixed dx = Fixed.Abs(player.position.X - doorPos.X) / Fixed.FromInt(10);
            Fixed dy = Fixed.Abs(player.position.Y - doorPos.Y) / Fixed.FromInt(10);
            Fixed distSq = (dx * dx) + (dy * dy);

            // Convert collider radius to Fixed and square it
            Fixed radius = Fixed.FromFloat(colliderRadius/10);
            Fixed radiusSq = radius * radius;

            // Determine overlap using squared values
            if (distSq > radiusSq)
            {
                //player is out of range
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

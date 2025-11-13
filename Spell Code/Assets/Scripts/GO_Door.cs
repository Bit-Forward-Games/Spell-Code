using UnityEngine;

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
            if (Mathf.Abs(gameObject.transform.position.x - player.position.x) > player.playerWidth/2)
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

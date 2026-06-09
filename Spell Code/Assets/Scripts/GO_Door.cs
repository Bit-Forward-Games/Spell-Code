using BestoNet.Types;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
public class GO_Door : MonoBehaviour
{
    Animator animator;
    bool isOpen = false;
    public bool isPrimed = true;
    public bool soloModes = true;
    float colliderRadius = 36;
    private readonly bool[] onlineEntrySnapshotSent = new bool[4];
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

            // A disconnected player is gone from the match and will never reach the door;
            // don't block the transition waiting on them.
            if (player == null || !player.isConnected)
            {
                continue;
            }

            if (!IsPlayerInsideDoor(player))
            {
                //player is out of range
                isPrimed = true;
                return false;

            }
        }

        //all players are in range and the door is open, then return true
        return true;
    }

    public void BroadcastSnapshotForNewOnlineEntries(bool isRollback)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.isOnlineMatchActive || !isOpen)
        {
            ResetOnlineEntrySnapshotSent();
            return;
        }

        if (isRollback)
        {
            return;
        }

        bool newEntryDetected = false;
        int firstNewEntryPid = -1;

        for (int i = 0; i < gameManager.playerCount && i < onlineEntrySnapshotSent.Length; i++)
        {
            PlayerController player = gameManager.players[i];
            if (player == null || !player.isConnected)
            {
                continue;
            }
            bool isInsideDoor = IsPlayerInsideDoor(player);

            if (isInsideDoor && !onlineEntrySnapshotSent[i])
            {
                onlineEntrySnapshotSent[i] = true;
                newEntryDetected = true;
                if (firstNewEntryPid < 0 && player != null)
                {
                    firstNewEntryPid = player.pID;
                }
            }
            else if (!isInsideDoor)
            {
                onlineEntrySnapshotSent[i] = false;
            }
        }

        if (newEntryDetected)
        {
            string reason = firstNewEntryPid > 0 ? $"go door enter P{firstNewEntryPid}" : "go door enter";
            gameManager.BroadcastAuthoritativeOnlineStateSnapshot(reason);
        }
    }

    private bool IsPlayerInsideDoor(PlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        FixedVec2 doorPos = FixedVec2.FromFloat(transform.position.x, transform.position.y);
        // Compute squared distance (avoid square root):
        Fixed dx = Fixed.Abs(player.position.X - doorPos.X) / Fixed.FromInt(100);
        Fixed dy = Fixed.Abs(player.position.Y - doorPos.Y) / Fixed.FromInt(100);
        Fixed distSq = (dx * dx) + (dy * dy);

        // Convert collider radius to Fixed and square it
        Fixed radius = Fixed.FromFloat(colliderRadius / 100);
        Fixed radiusSq = radius * radius;

        // Determine overlap using squared values
        return distSq <= radiusSq && player.isGrounded;
    }

    private void ResetOnlineEntrySnapshotSent()
    {
        for (int i = 0; i < onlineEntrySnapshotSent.Length; i++)
        {
            onlineEntrySnapshotSent[i] = false;
        }
    }

    public bool CheckOpenDoor()
    {

        if (GameManager.Instance.playerCount > 0)
        {
            isOpen = true;
            soloModes = GameManager.Instance.playerCount == 1;
        }
        else
        {
            isOpen = false;
            soloModes = true;
        }

        if(animator == null)
        {
            return isOpen;
        }
        
        if (GameManager.Instance.playerCount != animator.GetInteger("numPlayers"))
        {
            animator.SetInteger("numPlayers", GameManager.Instance.playerCount);
        }
        return isOpen;
    }
}

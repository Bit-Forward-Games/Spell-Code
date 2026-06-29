using BestoNet.Types;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
public class Exit_Door : MonoBehaviour
{
    float colliderRadius = 36;
    public int doorID = 0;
    bool isOpen = false;
    public bool isPrimed = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CheckOpenDoor();
        if (CheckAllPlayersReady() && isPrimed)
        {
            if (doorID == 1)
            {
                isPrimed = false;
                GameManager.Instance.tempUI.SetSoloMenuActive(true);
            }
            if (doorID == 2)
            {
                
                isPrimed = false;

                
            }
        }
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

        if (doorID == 0)
        {
            GameManager.Instance.sceneManager.MainMenu();
            return true;
        }

        //all players are in range and the door is open, then return true
        // GameManager.Instance.ExecuteOrder66();
        // SceneManager.LoadScene("MainMenu");
        return true;
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

    public bool CheckOpenDoor()
    {

        if (GameManager.Instance.playerCount > 0)
        {
            isOpen = true;
            //soloModes = GameManager.Instance.playerCount == 1;
        }
        else
        {
            isOpen = false;
            //soloModes = true;
        }

        return isOpen;
    }
}


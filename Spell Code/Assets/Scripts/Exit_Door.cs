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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CheckAllPlayersReady();
    }

    public bool CheckAllPlayersReady()
    {
        if(Time.timeScale == 0)
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
            Fixed dx = Fixed.Abs(player.position.X - doorPos.X) / Fixed.FromInt(100);
            Fixed dy = Fixed.Abs(player.position.Y - doorPos.Y) / Fixed.FromInt(100);
            Fixed distSq = (dx * dx) + (dy * dy);

            // Convert collider radius to Fixed and square it
            Fixed radius = Fixed.FromFloat(colliderRadius / 100);
            Fixed radiusSq = radius * radius;

            // Determine overlap using squared values
            if (distSq > radiusSq)
            {
                //player is out of range
                return false;
            }
        }

        //all players are in range and the door is open, then return true
        // GameManager.Instance.ExecuteOrder66();
        // SceneManager.LoadScene("MainMenu");
        GameManager.Instance.sceneManager.MainMenu();
        return true;
    }
}

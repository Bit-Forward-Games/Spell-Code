using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpellCode_FloppyDisk : MonoBehaviour
{
    public Animator diskAnimator;
    //Bounds diskBounds;
    public string diskName;
    public Image shopSprite;
    public SpellFloppyDisplay diskDisplay;
    public PlayerController overlappingPlayer = null;

    public bool colliding;

    public float colliderRadius = 16f;

    private byte selectHoldCounter = 0;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.FindAllFloppyDisks();
        diskDisplay.GetComponent<SpellFloppyDisplay>().SetSpellFloppyDisplay(diskName);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        colliding = (CheckPlayerCollision() != null);


        if (colliding)
        {
            diskDisplay.canvasObject.GetComponent<Canvas>().enabled = true;

            diskDisplay.transform.position = new Vector3(transform.position.x, transform.position.y + 16f, transform.position.z);

            if (overlappingPlayer != null)
            {
                if (overlappingPlayer.input.ButtonStates[0] == ButtonState.Held)
                {
                    //overlappingPlayer.AddSpellToSpellList(diskName); //Change this when starting spells are proper
                    //diskDisplay.gameObject.SetActive(false);
                    selectHoldCounter++;
                }
                else
                {
                    selectHoldCounter = 0;
                }

                if (selectHoldCounter >= 60)
                {
                    overlappingPlayer.AddSpellToSpellList(diskName);
                    diskDisplay.canvasObject.GetComponent<Canvas>().enabled = false;
                    //GameManager.Instance.RemoveFloppyDisk(this); -----doesnt exist but maybe should
                    Destroy(gameObject);
                }
            }
            else
            {
                selectHoldCounter = 0;
            }

            
        }
        else
        {
            selectHoldCounter = 0;
            diskDisplay.canvasObject.GetComponent<Canvas>().enabled = false;    
        }
        diskDisplay.selectFill.fillAmount = selectHoldCounter / 60f;
    }

    public PlayerController CheckPlayerCollision()
    {
        PlayerController player;
        // check if all active players are within a certain range of the door
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            player = GameManager.Instance.players[i];

            FixedVec2 floppyPos = FixedVec2.FromFloat(transform.position.x, transform.position.y);
            // Compute squared distance (avoid square root):
            Fixed dx = Fixed.Abs(player.position.X - floppyPos.X) / Fixed.FromInt(10);
            Fixed dy = Fixed.Abs(player.position.Y - floppyPos.Y) / Fixed.FromInt(10);
            Fixed distSq = (dx * dx) + (dy * dy);

            // Convert collider radius to Fixed and square it
            Fixed radius = Fixed.FromFloat(colliderRadius / 10);
            Fixed radiusSq = radius * radius;

            // Determine overlap using squared values
            if (distSq < radiusSq)
            {
                overlappingPlayer = player;
                Debug.Log("Overlapping player ID: " + overlappingPlayer.pID);
                return player;
            }
        }
        overlappingPlayer = null;
        return null;
    }
}

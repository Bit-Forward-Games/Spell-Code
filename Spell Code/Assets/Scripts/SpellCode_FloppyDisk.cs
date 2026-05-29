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
    public SpellFloppyDisplay diskDisplay;
    public PlayerController overlappingPlayer = null;
    private SpriteRenderer sprite;
    public int ownerPID;

    public bool colliding;

    public float colliderRadius = 16f;

    private byte selectHoldCounter = 0;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        diskAnimator = GetComponent<Animator>();
        GameManager.Instance.FindAllFloppyDisks();
        diskDisplay.GetComponent<SpellFloppyDisplay>().SetSpellFloppyDisplay(diskName);
        sprite = GetComponent<SpriteRenderer>();
        for (int i = 0; i < SpellDictionary.Instance.spellDict[diskName].brands.Length; i++)
        {
            if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.VWave)
            {
                diskAnimator.Play("FloppySpin");
            }
            if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.Killeez)
            {
                diskAnimator.Play("FloppySpinKilleez");
            }
            if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.DemonX)
            {
                diskAnimator.Play("FloppySpinDemonX");
            }
            if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.BigStox)
            {
                diskAnimator.Play("FloppySpinBigStox");
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive)
        {
            return;
        }
        colliding = CheckPlayerCollision() != null;


        if (colliding && overlappingPlayer.pID == ownerPID)
        {
            if (!diskDisplay.canvasObject.GetComponent<Canvas>().enabled)
            {
                diskDisplay.StartFloppyDisplay();
                diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID-1);
            }
            

            //diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID-1);

            if (overlappingPlayer != null)
            {
                if (overlappingPlayer.input.ButtonStates[0] == ButtonState.Held)
                {
                    selectHoldCounter++;
                }
                else
                {
                    selectHoldCounter = 0;
                }
                //this will show description
                if (overlappingPlayer.input.ButtonStates[0] == ButtonState.Released && selectHoldCounter < 60)
                {
                    diskDisplay.showDesc = !diskDisplay.showDesc;
                    diskDisplay.FloppyDisplayUpdate();
                }

                if (selectHoldCounter >= 60)
                {
                    if (overlappingPlayer.AddSpellToSpellList(diskName))
                    {
                        Debug.Log("Player " + ownerPID + " has acquired: " + diskName);
                        //if (SceneManager.GetActiveScene().name != "Tutorial")
                        //{
                            diskDisplay.canvasObject.GetComponent<Canvas>().enabled = false;
                            //GameManager.Instance.RemoveFloppyDisk(this); -----doesnt exist but maybe should
                            Destroy(gameObject);
                        //}
                    }
                    else
                    {
                        selectHoldCounter = 0;
                    }
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
        diskDisplay.selectFill.fillAmount = GetFillPercent();
    }

    public void SimulateOnline(ulong[] inputs, bool isRealFrame)
    {
        colliding = (CheckPlayerCollision() != null);

        if (colliding && overlappingPlayer.pID == ownerPID)
        {
            if (isRealFrame)
            {
                diskDisplay.canvasObject.GetComponent<Canvas>().enabled = true;
                diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID - 1);
            }

            InputSnapshot inputSnapshot = InputConverter.ConvertFromLong(5);
            int ownerIndex = ownerPID - 1;
            if (inputs != null && ownerIndex >= 0 && ownerIndex < inputs.Length)
            {
                inputSnapshot = InputConverter.ConvertFromLong(inputs[ownerIndex]);
            }

            if (inputSnapshot.ButtonStates[0] == ButtonState.Held)
            {
                selectHoldCounter++;
            }
            else
            {
                selectHoldCounter = 0;
            }

            if (selectHoldCounter >= 60)
            {
                if (HasOwnerAlreadyChosenOnlineSpell())
                {
                    selectHoldCounter = 0;
                    return;
                }

                if (overlappingPlayer.AddSpellToSpellList(diskName))
                {
                    if (SceneManager.GetActiveScene().name == "Shop")
                    {
                        overlappingPlayer.chosenSpell = true;
                    }

                    if (isRealFrame)
                    {
                        diskDisplay.canvasObject.GetComponent<Canvas>().enabled = false;
                    }
                    gameObject.SetActive(false);
                    if (isRealFrame)
                    {
                        GameManager.Instance?.BroadcastAuthoritativeOnlineStateSnapshot($"floppy pickup P{ownerPID} {diskName}");
                    }
                    Destroy(gameObject);
                }
                else
                {
                    selectHoldCounter = 0;
                }
            }
        }
        else
        {
            selectHoldCounter = 0;
            if (isRealFrame)
            {
                diskDisplay.canvasObject.GetComponent<Canvas>().enabled = false;
            }
        }

        if (isRealFrame)
        {
            diskDisplay.selectFill.fillAmount = GetFillPercent();
        }
    }

    private bool HasOwnerAlreadyChosenOnlineSpell()
    {
        if (GameManager.Instance == null || !GameManager.Instance.isOnlineMatchActive || overlappingPlayer == null)
        {
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == "MainMenu")
        {
            return overlappingPlayer.spellList.Count > 0;
        }

        if (activeScene.name == "Shop")
        {
            return overlappingPlayer.chosenSpell || overlappingPlayer.spellList.Count >= 6;
        }

        return false;
    }

    public byte GetSelectHoldCounter()
    {
        return selectHoldCounter;
    }

    public void SetSelectHoldCounter(byte value)
    {
        selectHoldCounter = value;
    }

    private float GetFillPercent()
    {
        float percent = selectHoldCounter / 60f;
        float normalizedLogarithmicfillPercent = Mathf.Clamp01(Mathf.Log10(percent)+1);
        return normalizedLogarithmicfillPercent;
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
            Fixed dx = Fixed.Abs(player.position.X - floppyPos.X) / Fixed.FromInt(100);
            Fixed dy = Fixed.Abs(player.position.Y - floppyPos.Y) / Fixed.FromInt(100);
            Fixed distSq = (dx * dx) + (dy * dy);

            // Convert collider radius to Fixed and square it
            Fixed radius = Fixed.FromFloat(colliderRadius / 100);
            Fixed radiusSq = radius * radius;

            // Determine overlap using squared values
            if (distSq < radiusSq)
            {
                overlappingPlayer = player;
                //Debug.Log("Overlapping player ID: " + overlappingPlayer.pID);
                return player;
            }
        }
        overlappingPlayer = null;
        return null;
    }
}

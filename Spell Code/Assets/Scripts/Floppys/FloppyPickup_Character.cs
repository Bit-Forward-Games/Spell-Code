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

public class FloppyPickup_Character : MonoBehaviour
{
    public enum Moveset
    {
        DemonX_1,
        DemonX_2,
        BigStox_1,
        BigStox_2,
        Killeez_1,
        Killeez_2,
        VWave_1,
        Vwave_2
    }

    public Moveset moveset;
    public string[] setList = new string[6];

    public Animator diskAnimator;
    //Bounds diskBounds;
    public string diskName;
    public SpellFloppyDisplay diskDisplay;
    public PlayerController overlappingPlayer = null;
    private SpriteRenderer sprite;
    public int ownerPID;

    public bool colliding;

    public float colliderRadius = 18f;

    private byte selectHoldCounter = 0;

    private int timeToFill = 30;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        diskAnimator = GetComponent<Animator>();
        GameManager.Instance.FindAllFloppyDisks();
        diskDisplay.GetComponent<SpellFloppyDisplay>().SetSpellFloppyDisplay(diskName);
        sprite = GetComponent<SpriteRenderer>();
        // for (int i = 0; i < SpellDictionary.Instance.spellDict[diskName].brands.Length; i++)
        // {
        //     if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.VWave)
        //     {
        //         diskAnimator.Play("FloppySpin");
        //     }
        //     if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.Killeez)
        //     {
        //         diskAnimator.Play("FloppySpinKilleez");
        //     }
        //     if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.DemonX)
        //     {
        //         diskAnimator.Play("FloppySpinDemonX");
        //     }
        //     if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.BigStox)
        //     {
        //         diskAnimator.Play("FloppySpinBigStox");
        //     }
        //     if (SpellDictionary.Instance.spellDict[diskName].brands[i] == Brand.DarkWeb)
        //     {
        //         diskAnimator.Play("FloppySpinDarkWeb");
        //     }
        // }
        if (SpellDictionary.Instance.spellDict[diskName].brands[0] == Brand.VWave)
        {
            diskAnimator.Play("FloppySpin");
        }
        if (SpellDictionary.Instance.spellDict[diskName].brands[0] == Brand.Killeez)
        {
            diskAnimator.Play("FloppySpinKilleez");
        }
        if (SpellDictionary.Instance.spellDict[diskName].brands[0] == Brand.DemonX)
        {
            diskAnimator.Play("FloppySpinDemonX");
        }
        if (SpellDictionary.Instance.spellDict[diskName].brands[0] == Brand.BigStox)
        {
            diskAnimator.Play("FloppySpinBigStox");
        }
        if (SpellDictionary.Instance.spellDict[diskName].brands[0] == Brand.DarkWeb)
        {
            diskAnimator.Play("FloppySpinDarkWeb");
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
            if (!diskDisplay.IsDisplayCanvasEnabled())
            {
                diskDisplay.StartFloppyDisplay();
                diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID - 1);
            }

 
            //diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID-1);

            if (overlappingPlayer != null)
            {
                if (selectHoldCounter == timeToFill)
                {
                    diskDisplay.SetDescriptionVisible(!diskDisplay.showDesc, true);
                }
                if (overlappingPlayer.input.ButtonStates[0] == ButtonState.Held)
                {
                    selectHoldCounter++;
                }
                else if (overlappingPlayer.input.ButtonStates[0] == ButtonState.Released)
                {
                    if (selectHoldCounter < timeToFill)
                    {
                        if (overlappingPlayer.AddSpellToSpellList(diskName))
                        {
                            Debug.Log("Player " + ownerPID + " has acquired: " + diskName);

                            //play the floppy pick up sfx
                            SFX_Manager.Instance.PlaySound(Sounds.FLOPPY_PICK_UP, 1.0f, 1.0f);

                            //play the appropriate floppy pick up vfx
                            switch (SpellDictionary.Instance.spellDict[diskName].brands[0])
                            {
                                case Brand.VWave:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                                case Brand.DemonX:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                                case Brand.Killeez:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                                case Brand.BigStox:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                                case Brand.DarkWeb:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DARKWEB_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                                default:
                                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                    break;
                            }

                            //if (SceneManager.GetActiveScene().name != "Tutorial")
                            //{
                            diskDisplay.StopFloppyDisplay();
                            //GameManager.Instance.RemoveFloppyDisk(this); -----doesnt exist but maybe should
                            Destroy(gameObject);
                            //}
                        }
                        // else
                        // {
                        //     selectHoldCounter = 0;
                        // }
                    }
                    selectHoldCounter = 0;



                }
                else
                {
                    selectHoldCounter = 0;
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
            diskDisplay.StopFloppyDisplay();
        }
        diskDisplay.selectFill.fillAmount = GetFillPercent();
        diskDisplay.selectFill.color = GameManager.colors[diskDisplay.selectFill.fillAmount == 1 ? "purple" : "grey"];
    }

    public void SimulateOnline(ulong[] inputs, bool isRealFrame)
    {
        colliding = (CheckPlayerCollision() != null);

        if (colliding && overlappingPlayer.pID == ownerPID)
        {
            if (isRealFrame && !diskDisplay.IsDisplayCanvasEnabled())
            {
                diskDisplay.StartFloppyDisplay();
                diskDisplay.SetFloppyDisplayPosition(overlappingPlayer.pID - 1);
            }

            InputSnapshot inputSnapshot = InputConverter.ConvertFromLong(5);
            int ownerIndex = ownerPID - 1;
            if (inputs != null && ownerIndex >= 0 && ownerIndex < inputs.Length)
            {
                inputSnapshot = InputConverter.ConvertFromLong(inputs[ownerIndex]);
            }

            if (isRealFrame && selectHoldCounter == timeToFill)
            {
                diskDisplay.SetDescriptionVisible(!diskDisplay.showDesc, true);
            }

            if (inputSnapshot.ButtonStates[0] == ButtonState.Held)
            {
                selectHoldCounter++;
            }
            else if (inputSnapshot.ButtonStates[0] == ButtonState.Released)
            {
                if (selectHoldCounter < timeToFill)
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
                            diskDisplay.StopFloppyDisplay();
                            // Play the floppy pick-up sfx on real frames only (rollback resim
                            // replays this pickup and would otherwise replay the sound). Null-guarded
                            // so a missing SFX_Manager can't throw mid-simulation
                            if (SFX_Manager.Instance != null)
                            {
                                SFX_Manager.Instance.PlaySound(Sounds.FLOPPY_PICK_UP, 1.0f, 1.0f);

                                //play the appropriate floppy pick up vfx
                                switch (SpellDictionary.Instance.spellDict[diskName].brands[0])
                                {
                                    case Brand.VWave:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                    case Brand.DemonX:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                    case Brand.Killeez:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                    case Brand.BigStox:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                    case Brand.DarkWeb:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DARKWEB_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                    default:
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_PICKUP, new FixedVec2(Fixed.FromFloat(this.gameObject.transform.position.x), Fixed.FromFloat(this.gameObject.transform.position.y)), ownerPID);
                                        break;
                                }
                            }
                        }
                        gameObject.SetActive(false);
                        if (isRealFrame)
                        {
                            GameManager.Instance?.BroadcastAuthoritativeOnlineStateSnapshot($"floppy pickup P{ownerPID} {diskName}");
                        }
                        Destroy(gameObject);
                    }
                }

                selectHoldCounter = 0;
            }
            else
            {
                selectHoldCounter = 0;
            }
        }
        else
        {
            selectHoldCounter = 0;
            if (isRealFrame)
            {
                diskDisplay.StopFloppyDisplay();
            }
        }

        if (isRealFrame)
        {
            diskDisplay.selectFill.fillAmount = GetFillPercent();
            diskDisplay.selectFill.color = GameManager.colors[diskDisplay.selectFill.fillAmount == 1 ? "purple" : "grey"];
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

    public bool IsDescriptionVisible()
    {
        return diskDisplay != null && diskDisplay.showDesc;
    }

    public void SetDescriptionVisible(bool visible, bool animate)
    {
        if (diskDisplay != null)
        {
            diskDisplay.SetDescriptionVisible(visible, animate);
        }
    }

    private float GetFillPercent()
    {
        float percent = selectHoldCounter / (float)timeToFill;
        float normalizedLogarithmicfillPercent = Mathf.Clamp01(Mathf.Log10(percent) + 1);
        return normalizedLogarithmicfillPercent;
    }

    public PlayerController CheckPlayerCollision()
    {
        PlayerController player;
        if (GameManager.Instance.playerCount > 0)
        {
            // check if all active players are within a certain range of the door
            for (int i = 0; i < GameManager.Instance.playerCount; i++)
            {
                player = GameManager.Instance.players[i];
                if (player == null || !GameManager.Instance.IsPlayerSlotConnected(i))
                {
                    continue;
                }

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
        }
        overlappingPlayer = null;
        return null;
    }

    //Return specific setlist for showdown @patrick
    //0 = DemonX_1, 1 = DemonX_2, 2 = BigStox_1, 3 = BigStox_2, 4 = Killeez_1, 5 = Killeez_2, 6 = VWave_1, 7 = Vwave_2
    public string[] SetCharacter(int movesetNum)
    {
        moveset = (Moveset)movesetNum;

        switch (moveset)
        {
            case Moveset.DemonX_1:
                diskName = "DemonX_1";
                setList[0] = "Amon Slash";
                setList[1] = "Asuran Blades";
                setList[2] = "Bifrons Blade";
                setList[3] = "Abbadon Uppercut";
                setList[4] = "Hell-Chain Sweep";
                setList[5] = "Demonic Descent";
                return setList;
            case Moveset.DemonX_2:
                diskName = "DemonX_2";
                setList[0] = "Rip and Tear";
                setList[1] = "Jigoku Flash Step";
                setList[2] = "Hell Wave Fist";
                setList[3] = "Brimstone Cyclone Kick";
                setList[4] = "Hellish Reposte";
                setList[5] = "Combo Demon";
                return setList;
            case Moveset.BigStox_1:
                diskName = "BigStox_1";
                setList[0] = "Use The Card";
                setList[1] = "Quarter Report";
                setList[2] = "Coin Toss";
                setList[3] = "Get A Job";
                setList[4] = "Blue Chip Trader";
                setList[5] = "Let It Ride";
                return setList;
            case Moveset.BigStox_2:
                diskName = "BigStox_2";
                setList[0] = "Cash Out";
                setList[1] = "Bailout";
                setList[2] = "Loaded Dice";
                setList[3] = "Trap Card Trick";
                setList[4] = "Lucky Break";
                setList[5] = "Hot Streak";
                return setList;
            case Moveset.Killeez_1:
                diskName = "Killeez_1";
                setList[0] = "Blade Of Ares";
                setList[1] = "Might Of Zeus";
                setList[2] = "Sun Of Apollo";
                setList[3] = "Trident Of Poseidon";
                setList[4] = "Boots Of Hermes";
                setList[5] = "Rod Of Asclepius";
                return setList;
            case Moveset.Killeez_2:
                diskName = "Killeez_2";
                setList[0] = "Gift Of Prometheus";
                setList[1] = "Hourglass Of Chronos";
                setList[2] = "Helm Of Hades";
                setList[3] = "Armory Of Hephaestus";
                setList[4] = "Aegis Of Athena";
                setList[5] = "Quiver Of Artemis";
                return setList;
            case Moveset.VWave_1:
                diskName = "VWave_1";
                setList[0] = "Skillshot Slash";
                setList[1] = "Reload Shot";
                setList[2] = "Pong Shot";
                setList[3] = "Trickshot Alley";
                setList[4] = "Mine Crafter";
                setList[5] = "No-Scope Shot";
                return setList;
            case Moveset.Vwave_2:
                diskName = "Vwave_2";
                setList[0] = "Shot Reflector";
                setList[1] = "Tele-Frag Prism";
                setList[2] = "Get Over Here";
                setList[3] = "Sickle Of The Night";
                setList[4] = "Crossmap Clip";
                setList[5] = "Back To Basics";
                return setList;
            default:
                return setList;
        }
    }
}



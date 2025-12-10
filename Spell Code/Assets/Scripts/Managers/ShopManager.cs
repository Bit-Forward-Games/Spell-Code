using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;

public class ShopManager : MonoBehaviour
{
    public Canvas shop;
    private GameManager gameManager;

    public System.Random myRandom;

    private bool allPlayersChosen = false;
    private bool backToGameplay = false;

    public InputSnapshot[] inputSnapshots = new InputSnapshot[4];

    public Image p1_spellCard;
    public Image p2_spellCard;
    public Image p3_spellCard;
    public Image p4_spellCard;

    public List<string> spells;

    [SerializeField]
    private List<string> p1_choices;
    [SerializeField]
    private List<string> p2_choices;
    [SerializeField]
    private List<string> p3_choices;
    [SerializeField]
    private List<string> p4_choices;

    private int p1_index = 0;
    private int p2_index = 0;
    private int p3_index = 0;
    private int p4_index = 0;

    public TextMeshProUGUI p1_spellText;
    public TextMeshProUGUI p2_spellText;
    public TextMeshProUGUI p3_spellText;
    public TextMeshProUGUI p4_spellText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

        myRandom = new System.Random(UnityEngine.Random.Range(0, 10000));
        Debug.Log("SHOP ENTERED");

        if (gameManager.players[2] == null)
        {
            p3_spellCard.enabled = false;
        }
        if (gameManager.players[3] == null)
        {
            p4_spellCard.enabled = false;
        }

        GenerateSpellChoices();

        p1_index = 0;
        p1_spellCard.sprite = SpellDictionary.Instance.spellDict[p1_choices[p1_index]].shopSprite;

        p2_index = 0;
        p2_spellCard.sprite = SpellDictionary.Instance.spellDict[p2_choices[p2_index]].shopSprite;

        if (gameManager.players[2] != null)
        {
            p3_index = 0;
            p3_spellCard.sprite = SpellDictionary.Instance.spellDict[p3_choices[p3_index]].shopSprite;
        }

        if (gameManager.players[3] != null)
        {
            p4_index = 0;
            p4_spellCard.sprite = SpellDictionary.Instance.spellDict[p4_choices[p4_index]].shopSprite;
        }

        //StartCoroutine(Shop());
    }

    public void ShopUpdate(ulong[] playerInputs)
    {
        for (int i = 0; i < playerInputs.Length; i++)
        {
            inputSnapshots[i] = InputConverter.ConvertFromLong(playerInputs[i]);
        }
        
        //player 1 stuff
        if (gameManager.players[0].chosenSpell == false)
        {
            //cycle spells (spellWeave button)
            if (inputSnapshots[0].ButtonStates[0] == ButtonState.Pressed)
            {
                Debug.Log("p1 pressed cycle spell");
                if (p1_index == 2)
                {
                    p1_index = 0;
                }
                else
                {
                    p1_index++;
                }

                p1_spellCard.sprite = SpellDictionary.Instance.spellDict[p1_choices[p1_index]].shopSprite;
            }

            //choose spell (jump button)
            if (inputSnapshots[0].ButtonStates[1] == ButtonState.Pressed)
            {
                Debug.Log("p1 chose a spell");
                GivePlayerSpell(0, p1_choices[p1_index]);
                gameManager.players[0].chosenSpell = true;
                p1_spellCard.enabled = false;
                inputSnapshots[0].SetNull();
            }
        }

        //player 2 stuff
        if (gameManager.players[1].chosenSpell == false)
        {
            //cycle spells (spellWeave button)
            if (inputSnapshots[1].ButtonStates[0] == ButtonState.Pressed)
            {
                Debug.Log("p2 pressed cycle spell");
                if (p2_index == 2)
                {
                    p2_index = 0;
                }
                else
                {
                    p2_index++;
                }

                p2_spellCard.sprite = SpellDictionary.Instance.spellDict[p2_choices[p2_index]].shopSprite;
            }

            //choose spell (jump button)
            if (inputSnapshots[1].ButtonStates[1] == ButtonState.Pressed)
            {
                Debug.Log("p2 chose a spell");
                GivePlayerSpell(1, p2_choices[p2_index]);
                gameManager.players[1].chosenSpell = true;
                p2_spellCard.enabled = false;
                inputSnapshots[1].SetNull();
            }
        }

        //if player 3 is not null
        if (gameManager.players[2] != null)
        {
            //player 3 stuff
            if (gameManager.players[2].chosenSpell == false)
            {
                //cycle spells (spellWeave button)
                if (inputSnapshots[2].ButtonStates[0] == ButtonState.Pressed)
                {
                    Debug.Log("p3 pressed cycle spell");
                    if (p3_index == 2)
                    {
                        p3_index = 0;
                    }
                    else
                    {
                        p3_index++;
                    }

                    p3_spellCard.sprite = SpellDictionary.Instance.spellDict[p3_choices[p3_index]].shopSprite;
                }

                //choose spell (jump button)
                if (inputSnapshots[2].ButtonStates[1] == ButtonState.Pressed)
                {
                    Debug.Log("p3 chose a spell");
                    GivePlayerSpell(2, p3_choices[p3_index]);
                    gameManager.players[2].chosenSpell = true;
                    p3_spellCard.enabled = false;
                    inputSnapshots[2].SetNull();
                }
            }
        }

        //if player 4 is not null
        if (gameManager.players[3] != null)
        {
            //player 4 stuff
            if (gameManager.players[3].chosenSpell == false)
            {
                //cycle spells (spellWeave button)
                if (inputSnapshots[3].ButtonStates[0] == ButtonState.Pressed)
                {
                    Debug.Log("p4 pressed cycle spell");
                    if (p4_index == 2)
                    {
                        p4_index = 0;
                    }
                    else
                    {
                        p4_index++;
                    }

                    p4_spellCard.sprite = SpellDictionary.Instance.spellDict[p4_choices[p4_index]].shopSprite;
                }

                //choose spell (jump button)
                if (inputSnapshots[3].ButtonStates[1] == ButtonState.Pressed)
                {
                    Debug.Log("p4 chose a spell");
                    GivePlayerSpell(3, p4_choices[p4_index]);
                    gameManager.players[3].chosenSpell = true;
                    p4_spellCard.enabled = false;
                    inputSnapshots[3].SetNull();
                }
            }
        }

        //escape logic
        int playersChosen = 0;
        if (!allPlayersChosen)
        {
            foreach (PlayerController player in gameManager.players)
            {
                if (player != null)
                {
                    if (player.chosenSpell) { playersChosen++; }
                }
            }

            if (playersChosen == gameManager.playerCount)
            {
                allPlayersChosen = true;
            }
            else
            {
                playersChosen = 0;
            }
        }
        if (allPlayersChosen && !backToGameplay)
        {
            foreach (PlayerController player in gameManager.players)
            {
                if (player != null)
                {
                    player.playerNum.enabled = true;
                    player.inputDisplay.enabled = true;
                }
            }
            backToGameplay = true;
            StartCoroutine(Shop());
        }
    }


    public IEnumerator Shop()
    {
        for (int i = 0; i < gameManager.playerCount; i++)
        {
            gameManager.players[i].chosenSpell = false;
        }
        gameManager.prevSceneWasShop = true;
        yield return new WaitForSeconds(1);
        GameManager.Instance.isRunning = true;

        //make the next stage random but different from the last stage
        gameManager.LoadRandomGameplayStage();

    }

    public string RandomizeSpell(int index)
    {
        //list of all spells in dictionary
        spells = new List<string>();

        //list of spells in player[index]'s spellbook
        List<string> playerSpells = new List<string>();

        //fill list of all spells in dictionary
        foreach (var item in SpellDictionary.Instance.spellDict)
        {
            spells.Add(item.Key);
        }

        //fill list of specific player's spells
        for (int i = 0; i < gameManager.players[index].spellList.Count; i++)
        {
            playerSpells.Add(gameManager.players[index].spellList[i].spellName);
        }

        //remove spell since it doesn't actually really exist
        spells.Remove("Active_Spell_4");

        //get a random spell
        int randomInt = myRandom.Next(0, spells.Count);
        string spellToAdd = spells[randomInt];

        //if the player doesn't have the spell, return it
        //if (!playerSpells.Contains(spellToAdd))
        //{
            //return spellToAdd;
        //}

        if (index == 0 && !p1_choices.Contains(spellToAdd))
        {
            return spellToAdd;
        }
        if (index == 1 && !p2_choices.Contains(spellToAdd))
        {
            return spellToAdd;
        }
        if (index == 2 && !p3_choices.Contains(spellToAdd))
        {
            return spellToAdd;
        }
        if (index == 3 && !p4_choices.Contains(spellToAdd))
        {
            return spellToAdd;
        }

        //if the player does have the spell, call this function again to get a new spell
        Debug.Log("player " + (index + 1) + " already has " + spellToAdd);
        return RandomizeSpell(index);
    }

    /// <summary>
    /// Grant a spell to a specific player
    /// </summary>
    /// <param name="index">Player to give the spell to</param>
    public void GivePlayerSpell(int index, string spell)
    {        
        
        Debug.Log("Giving player " + (index + 1) + " " + spell);
        gameManager.players[index].AddSpellToSpellList(spell);

        if (index == 0)
        {
            p1_spellText.text = "player " + (index + 1) + " acquired: " + spell + "\n";
        }
        if (index == 1)
        {
            p2_spellText.text = "player " + (index + 1) + " acquired: " + spell + "\n";
        }
        if (index == 2)
        {
            p3_spellText.text = "player " + (index + 1) + " acquired: " + spell + "\n";
        }
        if (index == 3)
        {
            p4_spellText.text = "player " + (index + 1) + " acquired: " + spell + "\n";
        }

    }

    //generates the spell choices for all players, in the future it will be randomized
    public void GenerateSpellChoices()
    {
        p1_choices = new List<string>();
        p2_choices = new List<string>();
        p3_choices = new List<string>();
        p4_choices = new List<string>();

        
        p1_choices.Add(RandomizeSpell(0));
        p1_choices.Add(RandomizeSpell(0));
        p1_choices.Add(RandomizeSpell(0));

        p2_choices.Add(RandomizeSpell(1));
        p2_choices.Add(RandomizeSpell(1));
        p2_choices.Add(RandomizeSpell(1));

        if (gameManager.players[2] != null)
        {
            p3_choices.Add(RandomizeSpell(2));
            p3_choices.Add(RandomizeSpell(2));
            p3_choices.Add(RandomizeSpell(2));
        }

        if (gameManager.players[3] != null)
        {
            p4_choices.Add(RandomizeSpell(3));
            p4_choices.Add(RandomizeSpell(3));
            p4_choices.Add(RandomizeSpell(3));
        }
    }
}

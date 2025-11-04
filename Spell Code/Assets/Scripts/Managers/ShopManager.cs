using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;

public class ShopManager : MonoBehaviour
{
    public Canvas shop;
    private GameManager gameManager;
    public TextMeshProUGUI spellText;

    public System.Random myRandom;

    private bool allPlayersChosen = false;
    private bool backToGameplay = false;

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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

        myRandom = new System.Random(UnityEngine.Random.Range(0, 10000));
        Debug.Log("SHOP ENTERED");
        GenerateSpellChoices();

        //StartCoroutine(Shop());
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //player 1 stuff
        if (gameManager.players[0].chosenSpell == false)
        {
            //cycle spells (spellWeave button)
            if (gameManager.players[0].inputs.CodeAction.IsPressed())
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
            if (gameManager.players[0].inputs.JumpAction.IsPressed())
            {
                Debug.Log("p1 chose a spell");
                GivePlayerSpell(0, p1_choices[p1_index]);
                gameManager.players[0].chosenSpell = true;
                p1_spellCard.enabled = false;
            }
        }

        //player 2 stuff
        if (gameManager.players[1].chosenSpell == false)
        {
            //cycle spells (spellWeave button)
            if (gameManager.players[1].inputs.CodeAction.IsPressed())
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
            if (gameManager.players[1].inputs.JumpAction.IsPressed())
            {
                Debug.Log("p2 chose a spell");
                GivePlayerSpell(1, p2_choices[p2_index]);
                gameManager.players[1].chosenSpell = true;
                p2_spellCard.enabled = false;
            }
        }

        //if player 3 is not null
        if (gameManager.players[2] != null)
        {
            //player 3 stuff
            if (gameManager.players[2].chosenSpell == false)
            {
                //cycle spells (spellWeave button)
                if (gameManager.players[2].inputs.CodeAction.IsPressed())
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
                if (gameManager.players[2].inputs.JumpAction.IsPressed())
                {
                    Debug.Log("p3 chose a spell");
                    GivePlayerSpell(2, p3_choices[p3_index]);
                    gameManager.players[2].chosenSpell = true;
                    p3_spellCard.enabled = false;
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
                if (gameManager.players[3].inputs.CodeAction.IsPressed())
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
                if (gameManager.players[3].inputs.JumpAction.IsPressed())
                {
                    Debug.Log("p4 chose a spell");
                    GivePlayerSpell(3, p4_choices[p4_index]);
                    gameManager.players[3].chosenSpell = true;
                    p4_spellCard.enabled = false;
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
        if(allPlayersChosen && !backToGameplay)
        {
            backToGameplay = true;
            StartCoroutine(Shop());
        }
    }


    public IEnumerator Shop()
    {
        //for (int i = 0; i < gameManager.playerCount; i++)
        //{
        //    GivePlayerSpell(i);
        //}
        gameManager.prevSceneWasShop = true;
        yield return new WaitForSeconds(4);
        GameManager.Instance.isRunning = true;
        spellText.text = " ";
        SceneManager.LoadScene("Gameplay");

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
        if (!playerSpells.Contains(spellToAdd))
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

        //spellText.text += "player " + (index + 1) + " acquired: " + spell + "\n";

    }

    public void GenerateSpellChoices()
    {
        p1_choices = new List<string>();
        p2_choices = new List<string>();
        p3_choices = new List<string>();
        p4_choices = new List<string>();

        p1_choices.Add("JumpBoost");
        p1_choices.Add("SpeedBoost");
        p1_choices.Add("GiftOfPrometheus");

        p2_choices.Add("JumpBoost");
        p2_choices.Add("SpeedBoost");
        p2_choices.Add("GiftOfPrometheus");

        p3_choices.Add("JumpBoost");
        p3_choices.Add("SpeedBoost");
        p3_choices.Add("GiftOfPrometheus");

        p4_choices.Add("JumpBoost");
        p4_choices.Add("SpeedBoost");
        p4_choices.Add("GiftOfPrometheus");
    }
}

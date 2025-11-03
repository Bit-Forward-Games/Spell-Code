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

public class ShopManager : MonoBehaviour
{
    public Canvas shop;
    private GameManager gameManager;
    public TextMeshProUGUI spellText;

    public System.Random myRandom;

    public Image p1_spellCard;
    public Image p2_spellCard;
    public Image p3_spellCard;
    public Image p4_spellCard;

    public List<string> spells;

    private List<string> p1_choices;
    private List<string> p2_choices;
    private List<string> p3_choices;
    private List<string> p4_choices;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

        myRandom = new System.Random(UnityEngine.Random.Range(0, 10000));

        StartCoroutine(Shop());
    }

    // Update is called once per frame
    void Update()
    {
       
    }


    public IEnumerator Shop()
    {
        for (int i = 0; i < gameManager.playerCount; i++)
        {
            GivePlayerSpell(i);
        }
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
    public void GivePlayerSpell(int index)
    {        
        string newSpell = RandomizeSpell(index);

        
        Debug.Log("Giving player " + (index + 1) + " " + newSpell);
        gameManager.players[index].AddSpellToSpellList(newSpell);

        spellText.text += "player " + (index + 1) + " acquired: " + newSpell + "\n";

    }

    public void GenerateSpellChoices()
    {
        p1_choices = new List<string>();
        p2_choices = new List<string>();
        p3_choices = new List<string>();
        p4_choices = new List<string>();

        p1_choices.Add(RandomizeSpell(0));
    }
}

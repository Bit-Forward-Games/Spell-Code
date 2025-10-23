using UnityEngine;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq;

public class ShopManager : MonoBehaviour
{
    public Canvas shop;
    private GameManager gameManager;
    public TextMeshProUGUI spellText;

    public System.Random myRandom;

    public List<string> spells;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

        myRandom = new System.Random(Random.Range(0, 10000));

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

    public string GrantSpell()
    {

        spells = new List<string>();

        foreach (var item in SpellDictionary.Instance.spellDict)
        {
            spells.Add(item.Key);
        }

        spells.Remove("Active_Spell_4");

        int randomInt = myRandom.Next(0, spells.Count);

        string spellToAdd = spells[randomInt];

        return spellToAdd;

    }

    public void GivePlayerSpell(int index)
    {        
        string newSpell = GrantSpell();

        //string fullSpellString = newSpell + "(Clone) " + newSpell;
        List<string> playerSpells = new List<string>();

        for (int i = 0; i < gameManager.players[index].spellList.Count; i++)
        {
            playerSpells.Add(gameManager.players[index].spellList[i].spellName);
        }

        if (!playerSpells.Contains(newSpell))//gameManager.players[index].spellList.Contains((SpellData)SpellDictionary.Instance.spellDict[newSpell]) == false)
        {
            Debug.Log("Giving player " + (index + 1) + " " + newSpell);
            gameManager.players[index].AddSpellToSpellList(newSpell);

            spellText.text += "player " + (index + 1) + " acquired: " + newSpell + "\n";

            return;
        }

        Debug.Log("player " + (index + 1) + " already has " + newSpell);
        GivePlayerSpell(index);

    }
}

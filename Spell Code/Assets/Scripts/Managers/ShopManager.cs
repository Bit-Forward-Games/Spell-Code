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

    public int rndSeed = 18246;


    public List<string> spells;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

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
            gameManager.players[i].GetComponent<SpriteRenderer>().enabled = false;
        }
        Random.InitState(rndSeed);
        GivePlayersSpell();
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

        int randomInt = Random.Range(0, spells.Count);

        string spellToAdd = spells[randomInt];

        return spellToAdd;

    }

    public void GivePlayersSpell()
    {
        for (int i = 0; i < gameManager.playerCount; i++)
        {
            string newSpell = GrantSpell();

            gameManager.players[i].AddSpellToSpellList(newSpell);

            spellText.text += "player " + (i + 1) + " acquired: " + newSpell + "\n";
        }
    }
}

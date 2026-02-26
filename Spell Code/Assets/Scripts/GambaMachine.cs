using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class GambaMachine : MonoBehaviour
{
    public Animator gambaAnimator;
    //Bounds diskBounds;
    public PlayerController ownerPlayer = null;
    public int ownerPID;
    private GameManager gameManager;
    private DataManager dataManager;

    public List<string> spells;

    [SerializeField]
    private List<GameObject> p1_floppys = new List<GameObject>();
    private List<GameObject> p2_floppys = new List<GameObject>();
    private List<GameObject> p3_floppys = new List<GameObject>();
    private List<GameObject> p4_floppys = new List<GameObject>();

    public HurtboxData hurtbox = new HurtboxData();
    public float colliderRadius = 16f;

    private byte resetTimer = 0;
    public int activatedCount = 0;

    public GameObject floppy;
    public Vector2[] diskLocations;
    private Scene activeScene;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;
        dataManager = DataManager.Instance;
        gameManager.FindAllFloppyDisks();
        hurtbox = new HurtboxData() { height = 36, width = 20, xOffset = -10, yOffset = 36};
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        gambaAnimator.SetBool("facingLeft", !facingRight);
        activeScene = SceneManager.GetActiveScene();
        if (ownerPlayer == null) { ownerPlayer = gameManager.players[ownerPID - 1]; }

        if (activeScene.name == "MainMenu")
        {
            if (ownerPlayer != null)
            {
                //delete other options after selecting one
                if (ownerPlayer.spellList.Count > 0)
                {
                    gambaAnimator.SetBool("isActive", false);

                    if (ownerPID == 1)
                    {
                        foreach (GameObject flop in p1_floppys) { Destroy(flop); }
                        p1_floppys.Clear();
                    }

                    if (ownerPID == 2)
                    {
                        foreach (GameObject flop in p2_floppys) { Destroy(flop); }
                        p2_floppys.Clear();
                    }

                    if (ownerPID == 3)
                    {
                        foreach (GameObject flop in p3_floppys) { Destroy(flop); }
                        p3_floppys.Clear();
                    }

                    if (ownerPID == 4)
                    {
                        foreach (GameObject flop in p4_floppys) { Destroy(flop); }
                        p4_floppys.Clear();
                    }
                }
            }
            if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
            {
                Debug.Log("Hitbox collision detected!");
                Debug.Log("LOBBY GAMBA");
                gambaAnimator.SetBool("isActive", false);

                if (ownerPID == 1) {
                    SpawnFloppyDisk(ownerPID, diskLocations[2], ownerPlayer.startingSpell); //real starter
                }
                if (ownerPID == 2) 
                { 
                    SpawnFloppyDisk(ownerPID, diskLocations[3], ownerPlayer.startingSpell); //real starter
                }
                if (ownerPID == 3) 
                { 
                    SpawnFloppyDisk(ownerPID, diskLocations[8], ownerPlayer.startingSpell); //real starter
                }
                if (ownerPID == 4) 
                { 
                    SpawnFloppyDisk(ownerPID, diskLocations[9], ownerPlayer.startingSpell); //real starter
                }
            }
        }

        else if (activeScene.name == "Shop")
        {
            if (ownerPlayer != null)
            {
                //delete other options after selecting one
                if (ownerPlayer.spellList.Count >= dataManager.totalRoundsPlayed + 1)
                {
                    activatedCount = 3;
                    gambaAnimator.SetBool("isActive", false);


                    if (ownerPID == 1)
                    {
                        foreach (GameObject flop in p1_floppys) { Destroy(flop); }
                        p1_floppys.Clear();
                    }
    
                    if (ownerPID == 2)
                    {
                        foreach (GameObject flop in p2_floppys) { Destroy(flop); }
                        p2_floppys.Clear();
                    }

                    if (ownerPID == 3)
                    {
                        foreach (GameObject flop in p3_floppys) { Destroy(flop); }
                        p3_floppys.Clear();
                    }

                    if (ownerPID == 4)
                    {
                        foreach (GameObject flop in p4_floppys) { Destroy(flop); }
                        p4_floppys.Clear();
                    }
                }

                //clear player choice lists and spawn 3 new floppys
                if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
                {
                    Debug.Log("Hitbox collision detected!");
                    Debug.Log("SHOP GAMBA");
                    gambaAnimator.SetBool("isActive", false);
                    activatedCount++;

                    if (ownerPID == 1)
                    {
                        foreach (GameObject flop in p1_floppys) { Destroy(flop); }
                        p1_floppys.Clear();
                        SpawnFloppyDisk(ownerPID, diskLocations[0]);
                        SpawnFloppyDisk(ownerPID, diskLocations[1]);
                        SpawnFloppyDisk(ownerPID, diskLocations[2]);
                    }
                    if (ownerPID == 2)
                    {
                        foreach (GameObject flop in p2_floppys) { Destroy(flop); }
                        p2_floppys.Clear();
                        SpawnFloppyDisk(ownerPID, diskLocations[3]);
                        SpawnFloppyDisk(ownerPID, diskLocations[4]);
                        SpawnFloppyDisk(ownerPID, diskLocations[5]);
                    }
                    if (ownerPID == 3)
                    {
                        foreach (GameObject flop in p3_floppys) { Destroy(flop); }
                        p3_floppys.Clear();
                        SpawnFloppyDisk(ownerPID, diskLocations[6]);
                        SpawnFloppyDisk(ownerPID, diskLocations[7]);
                        SpawnFloppyDisk(ownerPID, diskLocations[8]);
                    }
                    if (ownerPID == 4)
                    {
                        foreach (GameObject flop in p4_floppys) { Destroy(flop); }
                        p4_floppys.Clear();
                        SpawnFloppyDisk(ownerPID, diskLocations[9]);
                        SpawnFloppyDisk(ownerPID, diskLocations[10]);
                        SpawnFloppyDisk(ownerPID, diskLocations[11]);
                    }
                }
            }

            //in the future i want to keep track of the count so we can see how often players are rerolling their drops, but for now im tired
            if (gambaAnimator.GetBool("isActive") == false && activatedCount < 3)
            {
                Debug.Log("GAMBA RESET TIMER GOING");
                resetTimer++;

                if (resetTimer > 120)
                {
                    gambaAnimator.SetBool("isActive", true);
                    resetTimer = 0;
                }
                
            }
        }
    }


    public bool CheckHitboxCollision()
    {
        if(ownerPlayer == null || ownerPlayer.basicProjectileInstance == null ||
            !ProjectileManager.Instance.activeProjectiles.Contains(ownerPlayer.basicProjectileInstance.GetComponent<BaseProjectile>()))
        {
            return false;
        }

        return HitboxManager.Instance.ProcessSingleProjectileCollisison(
            ownerPlayer.basicProjectileInstance.GetComponent<BaseProjectile>(), 
            hurtbox, 
            FixedVec2.FromFloat(transform.position.x, transform.position.y), 
            true);
    }

    public void SpawnFloppyDisk(int ownerPID, Vector2 location, string name = "")
    {
        List<GameObject> floppys = new List<GameObject>();
        //random spell
        if (name == "")
        {
            spells = new List<string>();

            //list of spells in player[ownerPID]'s spellbook
            List<string> playerSpells = new List<string>();

            //fill list of all spells in dictionary
            foreach (var item in SpellDictionary.Instance.spellDict)
            {
                spells.Add(item.Key);
            }

            //fill list of specific player's spells
            for (int i = 0; i < gameManager.players[ownerPID - 1].spellList.Count; i++)
            {
                playerSpells.Add(gameManager.players[ownerPID - 1].spellList[i].spellName);
            }

            //Remove all passives for which the player has no actives for, or already has
            if (!gameManager.players[ownerPID - 1].vWave || playerSpells.Contains("Overclock"))
            {
                spells.Remove("Overclock");
            }
            if (!gameManager.players[ownerPID - 1].killeez || playerSpells.Contains("BootsOfHermes"))
            {
                spells.Remove("BootsOfHermes");
            }
            if (!gameManager.players[ownerPID - 1].DemonX || playerSpells.Contains("DemonicDescent"))
            {
                spells.Remove("DemonicDescent");
            }
            if (!gameManager.players[ownerPID - 1].bigStox || playerSpells.Contains("BlueChipTrader"))
            {
                spells.Remove("BlueChipTrader");
            }


            //get a random spell
            int randomInt = GameManager.Instance.seededRandom.Next(0, spells.Count);
            string spellToAdd = spells[randomInt];

            if (ownerPID == 1)
            {
                foreach (GameObject flop in p1_floppys)
                {
                    if (flop.GetComponent<SpellCode_FloppyDisk>().diskName == spellToAdd) 
                    {
                        Debug.Log("Player #" + ownerPID + " Duplicate disk:" + spellToAdd + ", rerolling");
                        SpawnFloppyDisk(ownerPID, location);
                        return;
                    }
                }
            }
            if (ownerPID == 2)
            {
                foreach (GameObject flop in p2_floppys)
                {
                    if (flop.GetComponent<SpellCode_FloppyDisk>().diskName == spellToAdd)
                    {
                        Debug.Log("Player #" + ownerPID + " Duplicate disk:" + spellToAdd + ", rerolling");
                        SpawnFloppyDisk(ownerPID, location);
                        return;
                    }
                }
            }
            if (ownerPID == 3)
            {
                foreach (GameObject flop in p3_floppys)
                {
                    if (flop.GetComponent<SpellCode_FloppyDisk>().diskName == spellToAdd)
                    {
                        Debug.Log("Player #" + ownerPID + " Duplicate disk:" + spellToAdd + ", rerolling");
                        SpawnFloppyDisk(ownerPID, location);
                        return;
                    }
                }
            }
            if (ownerPID == 4)
            {
                foreach (GameObject flop in p4_floppys)
                {
                    if (flop.GetComponent<SpellCode_FloppyDisk>().diskName == spellToAdd)
                    {
                        Debug.Log("Player #" + ownerPID + " Duplicate disk:" + spellToAdd + ", rerolling");
                        SpawnFloppyDisk(ownerPID, location);
                        return;
                    }
                }
            }

            GameObject disk = Instantiate(floppy, location, Quaternion.identity);
            SpellCode_FloppyDisk info = disk.GetComponent<SpellCode_FloppyDisk>();
            info.diskName = spellToAdd;
            info.ownerPID = ownerPID;
            floppys.Add(disk);
            if (floppys.Count > 1)
            {
                Destroy(disk);
            }

            if (ownerPID == 1) 
            { 
                p1_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p1_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            
            if (ownerPID == 2) 
            {
                p2_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p2_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 3) 
            {
                p3_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p3_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 4) 
            {
                p4_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p4_floppys.IndexOf(disk) + ": " + info.diskName);
            }
        }

        //case where spell name is specified
        else
        {
            GameObject disk = Instantiate(floppy, location, Quaternion.identity);
            SpellCode_FloppyDisk info = disk.GetComponent<SpellCode_FloppyDisk>();
            disk.transform.position = new Vector3(disk.transform.position.x, disk.transform.position.y, -1);
            info.diskName = name;
            info.ownerPID = ownerPID;
            floppys.Add(disk);
            if (floppys.Count > 1)
            {
                Destroy(disk);
            }

            if (ownerPID == 1)
            {
                p1_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p1_floppys.IndexOf(disk) + ": " + info.diskName);
            }

            if (ownerPID == 2)
            {
                p2_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p2_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 3)
            {
                p3_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p3_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 4)
            {
                p4_floppys.Add(disk); Debug.Log("Player #" + ownerPID + ", Choice #" + p4_floppys.IndexOf(disk) + ": " + info.diskName);
            }
        }
    }
}

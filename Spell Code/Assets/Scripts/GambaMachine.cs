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

    public byte resetTimer = 0;
    public int activatedCount = 0;

    public GameObject floppy;
    public Vector2[] diskLocations;
    private Scene activeScene;
    public bool facingRight = true;

    private String[] startingSpells;
    private int startingSpellPos;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;
        dataManager = DataManager.Instance;
        gameManager.FindAllFloppyDisks();
        hurtbox = new HurtboxData() { height = 36, width = 20, xOffset = -10, yOffset = 36};

        startingSpells = new string[4] {"AmonSlash", "QuarterReport", "BladeOfAres", "SkillshotSlash" };
        if (ownerPID == 1) { startingSpellPos = 0; }
        if (ownerPID == 2) { startingSpellPos = 1; }
        if (ownerPID == 3) { startingSpellPos = 2; }
        if (ownerPID == 4) { startingSpellPos = 3; }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (gameManager.isOnlineMatchActive) return;

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
                        activatedCount = 3;
                    }

                    if (ownerPID == 2)
                    {
                        foreach (GameObject flop in p2_floppys) { Destroy(flop); }
                        p2_floppys.Clear();
                        activatedCount = 3;
                    }

                    if (ownerPID == 3)
                    {
                        foreach (GameObject flop in p3_floppys) { Destroy(flop); }
                        p3_floppys.Clear();
                        activatedCount = 3;
                    }

                    if (ownerPID == 4)
                    {
                        foreach (GameObject flop in p4_floppys) { Destroy(flop); }
                        p4_floppys.Clear();
                        activatedCount = 3;
                    }
                }
            }
            if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
            {
                Debug.Log("Hitbox collision detected!");
                Debug.Log("LOBBY GAMBA");
                gambaAnimator.SetBool("isActive", false);

                if (ownerPID == 1) {
                    foreach (GameObject flop in p1_floppys) { Destroy(flop); }
                    p1_floppys.Clear();
                    SpawnFloppyDisk(ownerPID, diskLocations[2], startingSpells[startingSpellPos]); //real starter
                }
                if (ownerPID == 2) 
                {
                    foreach (GameObject flop in p2_floppys) { Destroy(flop); }
                    p2_floppys.Clear();
                    SpawnFloppyDisk(ownerPID, diskLocations[3], startingSpells[startingSpellPos]); //real starter
                }
                if (ownerPID == 3) 
                {
                    foreach (GameObject flop in p3_floppys) { Destroy(flop); }
                    p3_floppys.Clear();
                    SpawnFloppyDisk(ownerPID, diskLocations[8], startingSpells[startingSpellPos]); //real starter
                }
                if (ownerPID == 4) 
                {
                    foreach (GameObject flop in p4_floppys) { Destroy(flop); }
                    p4_floppys.Clear();
                    SpawnFloppyDisk(ownerPID, diskLocations[9], startingSpells[startingSpellPos]); //real starter
                }

                startingSpellPos++;
                if (startingSpellPos > 3)
                {
                    startingSpellPos = 0;
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

    public void SimulateOnline(int ownerPlayerIndex)
    {
        activeScene = SceneManager.GetActiveScene();
        if (ownerPlayer == null) ownerPlayer = gameManager.players[ownerPID - 1];
        if (ownerPlayer == null) return;

        if (activeScene.name == "MainMenu")
        {
            if (ownerPlayer.spellList.Count > 0)
            {
                gambaAnimator.SetBool("isActive", false);
                ClearFloppysForPID(ownerPID);
            }

            if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
            {
                gambaAnimator.SetBool("isActive", false);
                SpawnFloppysForOwnerOnline();
            }
        }
        else if (activeScene.name == "Shop")
        {
            SimulateShopOnline();
        }

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

    private void SimulateShopOnline()
    {
        if (ownerPlayer == null) return;

        if (ownerPlayer.spellList.Count >= dataManager.totalRoundsPlayed + 1)
        {
            activatedCount = 3;
            gambaAnimator.SetBool("isActive", false);
            ClearFloppysForPID(ownerPID);
        }

        if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
        {
            Debug.Log("SHOP GAMBA ONLINE");
            gambaAnimator.SetBool("isActive", false);
            activatedCount++;

            ClearFloppysForPID(ownerPID);

            if (ownerPID == 1)
            {
                SpawnFloppyDisk(ownerPID, diskLocations[0]);
                SpawnFloppyDisk(ownerPID, diskLocations[1]);
                SpawnFloppyDisk(ownerPID, diskLocations[2]);
            }
            if (ownerPID == 2)
            {
                SpawnFloppyDisk(ownerPID, diskLocations[3]);
                SpawnFloppyDisk(ownerPID, diskLocations[4]);
                SpawnFloppyDisk(ownerPID, diskLocations[5]);
            }
            if (ownerPID == 3)
            {
                SpawnFloppyDisk(ownerPID, diskLocations[6]);
                SpawnFloppyDisk(ownerPID, diskLocations[7]);
                SpawnFloppyDisk(ownerPID, diskLocations[8]);
            }
            if (ownerPID == 4)
            {
                SpawnFloppyDisk(ownerPID, diskLocations[9]);
                SpawnFloppyDisk(ownerPID, diskLocations[10]);
                SpawnFloppyDisk(ownerPID, diskLocations[11]);
            }
        }

        if (gambaAnimator.GetBool("isActive") == false && activatedCount < 3)
        {
            resetTimer++;
            if (resetTimer > 120)
            {
                gambaAnimator.SetBool("isActive", true);
                resetTimer = 0;
            }
        }
    }

    private void ClearFloppysForPID(int pid)
    {
        List<GameObject> list = GetFloppyListForPID(pid);
        foreach (GameObject flop in list) { Destroy(flop); }
        list.Clear();
    }

    private List<GameObject> GetFloppyListForPID(int pid)
    {
        if (pid == 1) return p1_floppys;
        if (pid == 2) return p2_floppys;
        if (pid == 3) return p3_floppys;
        return p4_floppys;
    }

    private void SpawnFloppysForOwner()
    {
        if (activeScene.name != "MainMenu") return;

        if (ownerPID == 1)
        {
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

    private void SpawnFloppysForOwnerOnline()
    {
        if (activeScene.name != "MainMenu") return;

        int lobbyIndex = gameManager.GetLobbySpellIndex(ownerPID);
        string lobbySpell = startingSpells[lobbyIndex];
        gameManager.AdvanceLobbySpellIndex(ownerPID, startingSpells.Length);

        if (ownerPID == 1)
        {
            foreach (GameObject flop in p1_floppys) { Destroy(flop); }
            p1_floppys.Clear();
            SpawnFloppyDisk(ownerPID, diskLocations[2], startingSpells[startingSpellPos]); //real starter
        }
        if (ownerPID == 2)
        {
            foreach (GameObject flop in p2_floppys) { Destroy(flop); }
            p2_floppys.Clear();
            SpawnFloppyDisk(ownerPID, diskLocations[3], startingSpells[startingSpellPos]); //real starter
        }
        if (ownerPID == 3)
        {
            SpawnFloppyDisk(ownerPID, diskLocations[8], lobbySpell); //real starter
        }
        if (ownerPID == 4)
        {
            SpawnFloppyDisk(ownerPID, diskLocations[9], lobbySpell); //real starter
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
            List<string> removedSpells = new List<string>();

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

            //remove passive spell if the player has it
            foreach (var spell in playerSpells)
            {
                if (SpellDictionary.Instance.spellDict[spell].spellType == SpellType.Passive)
                {
                    Debug.Log("Dupe Passive: " + spell + " has been removed");
                    removedSpells.Add(spell);
                }
            }

            //remove passives from the pool if the player has no active for them
            foreach (var spell in spells)
            {
                if (!gameManager.players[ownerPID - 1].vWave)
                {
                    if (SpellDictionary.Instance.spellDict[spell].brands[0] == Brand.VWave && SpellDictionary.Instance.spellDict[spell].spellType == SpellType.Passive)
                    {
                        if (!removedSpells.Contains(spell))
                        {
                            removedSpells.Add(spell);
                            Debug.Log("VWave passive: " + spell + " has been removed");
                        }
                    }
                }
                if (!gameManager.players[ownerPID - 1].killeez)
                {
                    if (SpellDictionary.Instance.spellDict[spell].brands[0] == Brand.Killeez && SpellDictionary.Instance.spellDict[spell].spellType == SpellType.Passive)
                    {
                        if (!removedSpells.Contains(spell))
                        {
                            removedSpells.Add(spell);
                            Debug.Log("Killeez passive: " + spell + " has been removed");
                        }
                    }
                }
                if (!gameManager.players[ownerPID - 1].DemonX)
                {
                    if (SpellDictionary.Instance.spellDict[spell].brands[0] == Brand.DemonX && SpellDictionary.Instance.spellDict[spell].spellType == SpellType.Passive)
                    {
                        if (!removedSpells.Contains(spell))
                        {
                            removedSpells.Add(spell);
                            Debug.Log("DemonX passive: " + spell + " has been removed");
                        }
                    }
                }
                if (!gameManager.players[ownerPID - 1].bigStox)
                {
                    if (SpellDictionary.Instance.spellDict[spell].brands[0] == Brand.BigStox && SpellDictionary.Instance.spellDict[spell].spellType == SpellType.Passive)
                    {
                        if (!removedSpells.Contains(spell))
                        {
                            removedSpells.Add(spell);
                            Debug.Log("BigStox passive: " + spell + " has been removed");
                        }
                    }
                }
            }

            foreach (string spell in removedSpells)
            {
                if (spells.Contains(spell)) { spells.Remove(spell); }
            }

            //get a random spell
            int randomInt = GameManager.Instance.GetNextRandom(0, spells.Count);
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

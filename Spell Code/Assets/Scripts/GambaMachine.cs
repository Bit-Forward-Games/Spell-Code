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

        startingSpells = new string[4] { "Amon Slash", "Quarter Report", "Blade Of Ares", "Skillshot Slash" };
        ResetLobbyState();
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

            if (resetTimer > 60)
            {
                gambaAnimator.SetBool("isActive", true);
                resetTimer = 0;
            }
        }
    }

    public void SimulateOnline(int ownerPlayerIndex, bool isRollback = false)
    {
        activeScene = SceneManager.GetActiveScene();
        if (ownerPlayer == null) ownerPlayer = gameManager.players[ownerPID - 1];
        if (ownerPlayer == null) return;

        if (activeScene.name == "MainMenu")
        {
            if (ownerPlayer.spellList.Count > 0)
            {
                gambaAnimator.SetBool("isActive", false);
                if (!isRollback) ClearFloppysForPID(ownerPID);
            }

            if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
            {
                gambaAnimator.SetBool("isActive", false);
                SpawnFloppysForOwnerOnline(isRollback);
            }
        }
        else if (activeScene.name == "Shop")
        {
            SimulateShopOnline(isRollback);
        }

        if (activeScene.name != "Shop" && gambaAnimator.GetBool("isActive") == false && activatedCount < 3)
        {
            if (!isRollback) Debug.Log("GAMBA RESET TIMER GOING");
            resetTimer++;

            if (resetTimer > 120)
            {
                gambaAnimator.SetBool("isActive", true);
                resetTimer = 0;
            }
        }
    }

    private void SimulateShopOnline(bool isRollback = false)
    {
        if (ownerPlayer == null) return;

        if (ownerPlayer.spellList.Count >= dataManager.totalRoundsPlayed + 1)
        {
            activatedCount = 3;
            gambaAnimator.SetBool("isActive", false);
            if (!isRollback) ClearFloppysForPID(ownerPID);
        }

        if (CheckHitboxCollision() && gambaAnimator.GetBool("isActive"))
        {
            if (!isRollback) Debug.Log("SHOP GAMBA ONLINE");
            gambaAnimator.SetBool("isActive", false);
            activatedCount++;

            if (!isRollback) ClearFloppysForPID(ownerPID);

            if (ownerPID == 1) SpawnThreeFloppysOnline(1, diskLocations[0], diskLocations[1], diskLocations[2], isRollback);
            if (ownerPID == 2) SpawnThreeFloppysOnline(2, diskLocations[3], diskLocations[4], diskLocations[5], isRollback);
            if (ownerPID == 3) SpawnThreeFloppysOnline(3, diskLocations[6], diskLocations[7], diskLocations[8], isRollback);
            if (ownerPID == 4) SpawnThreeFloppysOnline(4, diskLocations[9], diskLocations[10], diskLocations[11], isRollback);
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

    private void SpawnFloppysForOwnerOnline(bool isRollback = false)
    {
        if (activeScene.name != "MainMenu") return;

        if (!isRollback)
        {
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
                SpawnFloppyDisk(ownerPID, diskLocations[8], startingSpells[startingSpellPos]); //real starter
            }
            if (ownerPID == 4)
            {
                SpawnFloppyDisk(ownerPID, diskLocations[9], startingSpells[startingSpellPos]); //real starter
            }
        }
        // No RNG consumed in this path (named spell), so no rollback RNG needed

        startingSpellPos++;
        if (startingSpellPos > 3)
        {
            startingSpellPos = 0;
        }
    }

    public void ResetLobbyState()
    {
        resetTimer = 0;
        activatedCount = 0;

        if (ownerPID == 1) { startingSpellPos = 0; }
        if (ownerPID == 2) { startingSpellPos = 1; }
        if (ownerPID == 3) { startingSpellPos = 2; }
        if (ownerPID == 4) { startingSpellPos = 3; }

        ClearFloppysForPID(ownerPID);

        if (gambaAnimator != null)
        {
            gambaAnimator.SetBool("isActive", true);
        }
    }

    public int GetStartingSpellPos()
    {
        return startingSpellPos;
    }

    public void SetStartingSpellPos(int value)
    {
        startingSpellPos = value;
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

    public GameObject SpawnFloppyDisk(int ownerPID, Vector2 location, string name = "", bool playVfx = true, bool logChoice = true)
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
            if (gameManager != null && gameManager.isOnlineMatchActive)
            {
                spells.Sort(StringComparer.Ordinal);
            }

            //fill list of specific player's spells
            for (int i = 0; i < gameManager.players[ownerPID - 1].spellList.Count; i++)
            {
                playerSpells.Add(gameManager.players[ownerPID - 1].spellList[i].spellName);
            }

            //remove possible dupes of various reasons
            foreach (var spell in playerSpells)
            {
                //remove passive spell if the player has it
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
                        return SpawnFloppyDisk(ownerPID, location, "", playVfx, logChoice);
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
                        return SpawnFloppyDisk(ownerPID, location, "", playVfx, logChoice);
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
                        return SpawnFloppyDisk(ownerPID, location, "", playVfx, logChoice);
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
                        return SpawnFloppyDisk(ownerPID, location, "", playVfx, logChoice);
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

            //play the floppy disk VFX depending on the disk brand
            if (playVfx)
            {
                switch (SpellDictionary.Instance.spellDict[info.diskName].brands[0])
                {
                    case Brand.VWave:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.DemonX:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.Killeez:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.BigStox:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    default:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                }
            }

            if (ownerPID == 1) 
            {
                p1_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p1_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            
            if (ownerPID == 2) 
            {
                p2_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p2_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 3) 
            {
                p3_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p3_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 4) 
            {
                p4_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p4_floppys.IndexOf(disk) + ": " + info.diskName);
            }

            return disk;
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

            //play the floppy disk VFX depending on the disk brand
            if (playVfx)
            {
                switch (SpellDictionary.Instance.spellDict[info.diskName].brands[0])
                {
                    case Brand.VWave:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.DemonX:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.Killeez:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.BigStox:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    default:
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                }
            }

            if (ownerPID == 1)
            {
                p1_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p1_floppys.IndexOf(disk) + ": " + info.diskName);
            }

            if (ownerPID == 2)
            {
                p2_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p2_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 3)
            {
                p3_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p3_floppys.IndexOf(disk) + ": " + info.diskName);
            }
            if (ownerPID == 4)
            {
                p4_floppys.Add(disk);
                if (logChoice) Debug.Log("Player #" + ownerPID + ", Choice #" + p4_floppys.IndexOf(disk) + ": " + info.diskName);
            }

            return disk;
        }
    }

    private void SpawnThreeFloppysOnline(int pid, Vector2 loc1, Vector2 loc2, Vector2 loc3, bool isRollback = false)
    {
        // Build pool once, removing already-spawned spells
        spells = new List<string>();
        foreach (var item in SpellDictionary.Instance.spellDict)
            spells.Add(item.Key);
        spells.Sort(StringComparer.Ordinal); // deterministic order for online

        // Remove passives the player doesn't qualify for
        List<string> removedSpells = new List<string>();
        PlayerController player = gameManager.players[pid - 1];
        foreach (var spell in spells)
        {
            SpellData data = SpellDictionary.Instance.spellDict[spell];
            if (data.spellType == SpellType.Passive && player.spellList.Any(s => s.spellName == spell))
                removedSpells.Add(spell);
            if (!player.vWave && data.brands[0] == Brand.VWave && data.spellType == SpellType.Passive)
                removedSpells.Add(spell);
            if (!player.killeez && data.brands[0] == Brand.Killeez && data.spellType == SpellType.Passive)
                removedSpells.Add(spell);
            if (!player.DemonX && data.brands[0] == Brand.DemonX && data.spellType == SpellType.Passive)
                removedSpells.Add(spell);
            if (!player.bigStox && data.brands[0] == Brand.BigStox && data.spellType == SpellType.Passive)
                removedSpells.Add(spell);
        }
        foreach (string s in removedSpells)
            spells.Remove(s);

        // Pick 3 unique spells with single RNG calls each, no recursion
        // RNG must be consumed identically during rollback to keep state in sync
        List<string> chosen = new List<string>();
        List<Vector2> locations = new List<Vector2> { loc1, loc2, loc3 };

        for (int i = 0; i < 3 && spells.Count > 0; i++)
        {
            List<string> available = spells.Where(s => !chosen.Contains(s)).ToList();
            if (available.Count == 0) break;

            int randomInt = GameManager.Instance.GetNextRandom(0, available.Count);
            string spellToAdd = available[randomInt];
            chosen.Add(spellToAdd);

            if (!isRollback)
            {
                SpawnFloppyDisk(pid, locations[i], spellToAdd); // use the named overload, no RNG
            }
        }
    }
}

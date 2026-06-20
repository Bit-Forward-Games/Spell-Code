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
    public bool isActive;
    //Bounds diskBounds;
    public PlayerController ownerPlayer = null;
    public int ownerPID;
    private GameManager gameManager;
    private DataManager dataManager;

    //expressely for tutorial
    public int machineID;

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
    public Vector2[] tutorialLocs;
    public Vector2[] trainingLocs;
    private Scene activeScene;
    public bool facingRight = true;

    private String[] startingSpells;
    private int startingSpellPos;

    public bool cooldown = true;

    //expressely for tutorial gamba
    private int counter = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;
        dataManager = DataManager.Instance;
        gameManager.FindAllFloppyDisks();
        hurtbox = new HurtboxData() { height = 36, width = 20, xOffset = -10, yOffset = 36};

        startingSpells = new string[4] { "Amon Slash", "Use The Card", "Blade Of Ares", "Skillshot Slash" };
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
                    isActive = false;

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
            if (isActive && CheckHitboxCollision())
            {
                Debug.Log("Hitbox collision detected!");
                Debug.Log("LOBBY GAMBA");

                //play the gamba hit sfx
                SFX_Manager.Instance.PlaySound(Sounds.GAMBA_HIT, 1.0f, 1.0f);

                isActive = false;

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
                    isActive = false;


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
                if (isActive && CheckHitboxCollision())
                {
                    Debug.Log("Hitbox collision detected!");
                    Debug.Log("SHOP GAMBA");

                    //play the gamba hit sfx
                    SFX_Manager.Instance.PlaySound(Sounds.GAMBA_HIT, 1.0f, 1.0f);

                    isActive = false;
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

        else if (activeScene.name == "Tutorial")
        {
            if (isActive && CheckHitboxCollision() && machineID == 1)
            {
                Debug.Log("Hitbox collision detected!");
                Debug.Log("TUTORIAL GAMBA");

                //play the gamba hit sfx
                SFX_Manager.Instance.PlaySound(Sounds.GAMBA_HIT, 1.0f, 1.0f);

                isActive = false;

                SpawnFloppyDisk(ownerPID, tutorialLocs[0], "Amon Slash");
            }
        }

        else if (activeScene.name == "TrainingGrounds")
        {

            if (isActive && CheckHitboxCollision() && machineID == 3)
            {
                foreach (GameObject flop in p1_floppys) { Destroy(flop); }
                p1_floppys.Clear();

                //play the gamba hit sfx
                SFX_Manager.Instance.PlaySound(Sounds.GAMBA_HIT, 1.0f, 1.0f);

                isActive = false;

                //1 = DemonX, 2 = BigStox, 3 = Killeez, 4 = VWave
                counter++;
                if (counter > 4) { counter = 1; }

                switch (counter)
                {
                    //DemonX
                    case 1:
                        SpawnFloppyDisk(ownerPID, trainingLocs[0], "Amon Slash");
                        SpawnFloppyDisk(ownerPID, trainingLocs[1], "Asuran Blades");
                        SpawnFloppyDisk(ownerPID, trainingLocs[2], "Bifrons Blade");
                        SpawnFloppyDisk(ownerPID, trainingLocs[3], "Abaddon Uppercut");
                        SpawnFloppyDisk(ownerPID, trainingLocs[4], "Rip And Tear");
                        SpawnFloppyDisk(ownerPID, trainingLocs[5], "Hell-Chain Sweep");
                        SpawnFloppyDisk(ownerPID, trainingLocs[6], "Demonic Descent");
                        break;
                    //BigStox
                    case 2:
                        SpawnFloppyDisk(ownerPID, trainingLocs[0], "Use The Card");
                        SpawnFloppyDisk(ownerPID, trainingLocs[1], "Quarter Report");
                        SpawnFloppyDisk(ownerPID, trainingLocs[2], "Coin Toss");
                        SpawnFloppyDisk(ownerPID, trainingLocs[3], "Get A Job");
                        SpawnFloppyDisk(ownerPID, trainingLocs[4], "Cash Out");
                        SpawnFloppyDisk(ownerPID, trainingLocs[5], "Blue Chip Trader");
                        SpawnFloppyDisk(ownerPID, trainingLocs[6], "Let It Ride");
                        break;
                    //Killeez
                    case 3:
                        SpawnFloppyDisk(ownerPID, trainingLocs[0], "Blade Of Ares");
                        SpawnFloppyDisk(ownerPID, trainingLocs[1], "Might Of Zeus");
                        SpawnFloppyDisk(ownerPID, trainingLocs[2], "Sun Of Apollo");
                        SpawnFloppyDisk(ownerPID, trainingLocs[3], "Trident Of Poseidon");
                        SpawnFloppyDisk(ownerPID, trainingLocs[4], "Gift Of Prometheus");
                        SpawnFloppyDisk(ownerPID, trainingLocs[5], "Boots Of Hermes");
                        SpawnFloppyDisk(ownerPID, trainingLocs[6], "Rod Of Asclepius");
                        break;
                    //VWave
                    case 4:
                        SpawnFloppyDisk(ownerPID, trainingLocs[0], "Skillshot Slash");
                        SpawnFloppyDisk(ownerPID, trainingLocs[1], "Reload Shot");
                        SpawnFloppyDisk(ownerPID, trainingLocs[2], "Pong Shot");
                        SpawnFloppyDisk(ownerPID, trainingLocs[3], "Trickshot Alley");
                        SpawnFloppyDisk(ownerPID, trainingLocs[4], "Shot Reflector");
                        SpawnFloppyDisk(ownerPID, trainingLocs[5], "Mine Crafter");
                        SpawnFloppyDisk(ownerPID, trainingLocs[6], "No-Scope Shot");
                        break;
                }

            }
        }

        //in the future i want to keep track of the count so we can see how often players are rerolling their drops, but for now im tired
        if (!isActive && activatedCount < 3 && cooldown)
        {
            //Debug.Log("GAMBA RESET TIMER GOING");
            resetTimer++;

            if (resetTimer > 60)
            {
                isActive = true;
                resetTimer = 0;
            }
        }

        gambaAnimator.SetBool("isActive", isActive);
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
                isActive = false;
                ClearFloppysForPID(ownerPID);
            }

            if (isActive && CheckHitboxCollision())
            {
                isActive = false;
                SpawnFloppysForOwnerOnline(isRollback);
            }
        }
        else if (activeScene.name == "Shop")
        {
            SimulateShopOnline(isRollback);
        }

        if (activeScene.name != "Shop" && !isActive && activatedCount < 3)
        {
            if (!isRollback) Debug.Log("GAMBA RESET TIMER GOING");
            resetTimer++;

            if (resetTimer > 120)
            {
                isActive = true;
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
            isActive = false;
            ClearFloppysForPID(ownerPID);
        }

        if (isActive && CheckHitboxCollision())
        {
            if (!isRollback) Debug.Log("SHOP GAMBA ONLINE");
            isActive = false;
            activatedCount++;

            ClearFloppysForPID(ownerPID);

            if (ownerPID == 1) SpawnThreeFloppysOnline(1, diskLocations[0], diskLocations[1], diskLocations[2], isRollback);
            if (ownerPID == 2) SpawnThreeFloppysOnline(2, diskLocations[3], diskLocations[4], diskLocations[5], isRollback);
            if (ownerPID == 3) SpawnThreeFloppysOnline(3, diskLocations[6], diskLocations[7], diskLocations[8], isRollback);
            if (ownerPID == 4) SpawnThreeFloppysOnline(4, diskLocations[9], diskLocations[10], diskLocations[11], isRollback);
        }

        if (!isActive && activatedCount < 3)
        {
            resetTimer++;
            if (resetTimer > 120)
            {
                isActive = true;
                resetTimer = 0;
            }
        }
    }

    private void ClearFloppysForPID(int pid)
    {
        List<GameObject> list = GetFloppyListForPID(pid);
        foreach (GameObject flop in list)
        {
            if (flop == null) continue;
            flop.SetActive(false);
            Destroy(flop);
        }
        list.Clear();
    }

    public void ClearTrackedFloppyReferences()
    {
        p1_floppys.Clear();
        p2_floppys.Clear();
        p3_floppys.Clear();
        p4_floppys.Clear();
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

        bool playVfx = !isRollback;
        bool logChoice = !isRollback;

        if (ownerPID == 1)
        {
            ClearFloppysForPID(ownerPID);
            SpawnFloppyDisk(ownerPID, diskLocations[2], startingSpells[startingSpellPos], playVfx, logChoice); //real starter
        }
        if (ownerPID == 2)
        {
            ClearFloppysForPID(ownerPID);
            SpawnFloppyDisk(ownerPID, diskLocations[3], startingSpells[startingSpellPos], playVfx, logChoice); //real starter
        }
        if (ownerPID == 3)
        {
            ClearFloppysForPID(ownerPID);
            SpawnFloppyDisk(ownerPID, diskLocations[8], startingSpells[startingSpellPos], playVfx, logChoice); //real starter
        }
        if (ownerPID == 4)
        {
            ClearFloppysForPID(ownerPID);
            SpawnFloppyDisk(ownerPID, diskLocations[9], startingSpells[startingSpellPos], playVfx, logChoice); //real starter
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
        
        isActive = true;
    }

    public void ResetShopState(PlayerController activeOwner, bool ownerCanUseShop)
    {
        ownerPlayer = activeOwner;
        resetTimer = 0;
        activatedCount = ownerCanUseShop ? 0 : 3;
        ClearFloppysForPID(ownerPID);
        
        isActive = ownerCanUseShop;
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

    private List<string> BuildAvailableSpellPool(int pid)
    {
        spells = new List<string>();

        if (SpellDictionary.Instance == null || SpellDictionary.Instance.spellDict == null)
        {
            return spells;
        }

        PlayerController player = gameManager.players[pid - 1];
        foreach (var item in SpellDictionary.Instance.spellDict)
        {
            if (!ShouldRemoveSpellFromPool(player, item.Key))
            {
                spells.Add(item.Key);
            }
        }

        if (gameManager != null && gameManager.isOnlineMatchActive)
        {
            spells.Sort(StringComparer.Ordinal);
        }

        return spells;
    }

    private bool ShouldRemoveSpellFromPool(PlayerController player, string spellName)
    {
        if (player == null ||
            SpellDictionary.Instance == null ||
            SpellDictionary.Instance.spellDict == null ||
            !SpellDictionary.Instance.spellDict.TryGetValue(spellName, out SpellData spellData) ||
            spellData == null)
        {
            return false;
        }

        if (player.HasReachedSpellCopyLimit(spellName))
        {
            Debug.Log("Copy cap reached: " + spellName + " has been removed");
            return true;
        }

        if (spellData.spellType == SpellType.Universal)
        {
            Debug.Log("Universal spell: " + spellName + " has been removed");
            return true;
        }

        if (spellData.spellType != SpellType.Passive)
        {
            return false;
        }

        Brand primaryBrand = spellData.brands != null && spellData.brands.Length > 0
            ? spellData.brands[0]
            : Brand.None;

        if (!player.vWave && primaryBrand == Brand.VWave)
        {
            Debug.Log("VWave passive: " + spellName + " has been removed");
            return true;
        }

        if (!player.killeez && primaryBrand == Brand.Killeez)
        {
            Debug.Log("Killeez passive: " + spellName + " has been removed");
            return true;
        }

        if (!player.DemonX && primaryBrand == Brand.DemonX)
        {
            Debug.Log("DemonX passive: " + spellName + " has been removed");
            return true;
        }

        if (!player.bigStox && primaryBrand == Brand.BigStox)
        {
            Debug.Log("BigStox passive: " + spellName + " has been removed");
            return true;
        }

        return false;
    }

    private void RemoveAlreadySpawnedChoicesFromPool(int pid)
    {
        List<GameObject> floppyList = GetFloppyListForPID(pid);
        for (int i = 0; i < floppyList.Count; i++)
        {
            GameObject floppyDisk = floppyList[i];
            if (floppyDisk == null)
            {
                continue;
            }

            FloppyPickup diskInfo = floppyDisk.GetComponent<FloppyPickup>();
            if (diskInfo != null)
            {
                spells.Remove(diskInfo.diskName);
            }
        }
    }

    public GameObject SpawnFloppyDisk(int ownerPID, Vector2 location, string name = "", bool playVfx = true, bool logChoice = true)
    {
        List<GameObject> floppys = new List<GameObject>();
        //random spell
        if (name == "")
        {
            BuildAvailableSpellPool(ownerPID);
            RemoveAlreadySpawnedChoicesFromPool(ownerPID);

            if (spells.Count == 0)
            {
                Debug.LogWarning("No valid spells left in the pool for this player.");
                return null;
            }

            //get a random spell
            int randomInt = GameManager.Instance.GetNextRandom(0, spells.Count);
            string spellToAdd = spells[randomInt];

            GameObject disk = Instantiate(floppy, location, Quaternion.identity);
            FloppyPickup info = disk.GetComponent<FloppyPickup>();
            info.diskName = spellToAdd;
            info.ownerPID = ownerPID;
            floppys.Add(disk);
            if (floppys.Count > 1)
            {
                Destroy(disk);
            }

            //WIP
            //play the new flppy disk VFX depending on disk brand
            if (playVfx)
            {
                switch (SpellDictionary.Instance.spellDict[info.diskName].brands[0])
                {
                    case Brand.VWave:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.VWAVE_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.DemonX:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.DEMONX_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.Killeez:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.KILLEEZ_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.BigStox:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.BIGSTOX_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    default:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.VWAVE_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                }
            }

            //play the floppy disk VFX depending on the disk brand
            //if (playVfx)
            //{
            //    switch (SpellDictionary.Instance.spellDict[info.diskName].brands[0])
            //    {
            //        case Brand.VWave:
            //            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
            //            break;
            //        case Brand.DemonX:
            //            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
            //            break;
            //        case Brand.Killeez:
            //            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
            //            break;
            //        case Brand.BigStox:
            //            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
            //            break;
            //        default:
            //            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
            //            break;
            //    }
            //}

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
            FloppyPickup info = disk.GetComponent<FloppyPickup>();
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
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.VWAVE_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.DemonX:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.DEMONX_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.Killeez:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.KILLEEZ_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    case Brand.BigStox:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.BIGSTOX_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_FLOPPY_SPAWN, FixedVec2.FromFloat(location.x, location.y) + FixedVec2.FromFloat(0f, 11.5f), ownerPID);
                        break;
                    default:
                        VFX_Manager.Instance.PlayTrailVisualEffect(VisualEffects.VWAVE_FLOPPY_ARC, this.gameObject.transform.position, location, 1f, 1f, ownerPID);
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
        BuildAvailableSpellPool(pid);

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

            SpawnFloppyDisk(pid, locations[i], spellToAdd, !isRollback, !isRollback); // use the named overload, no RNG
        }
    }
}

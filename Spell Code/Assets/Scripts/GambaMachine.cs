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

    public HurtboxData hurtbox = new HurtboxData();
    public float colliderRadius = 16f;

    private byte resetTimer = 0;
    public int activatedCount = 0;

    public GameObject floppy;
    private List<GameObject> floppys;
    public Vector2[] diskLocations;
    private Scene activeScene;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        activeScene = SceneManager.GetActiveScene();
        GameManager.Instance.FindAllFloppyDisks();
        hurtbox = new HurtboxData() { height = 36, width = 20, xOffset = -10, yOffset = 36};
        floppys = new List<GameObject>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (ownerPlayer == null) { ownerPlayer = GameManager.Instance.players[ownerPID - 1]; }
        if(CheckHitboxCollision())
        {
            Debug.Log("Hitbox collision detected!");
            if (activeScene.name == "MainMenu" && gambaAnimator.GetBool("isActive"))
            {
                gambaAnimator.SetBool("isActive", false);

                if (ownerPID == 1) { SpawnFloppyDisk(1, diskLocations[2], ownerPlayer.startingSpell); }
                if (ownerPID == 2) { SpawnFloppyDisk(1, diskLocations[3], ownerPlayer.startingSpell); }
                if (ownerPID == 3) { SpawnFloppyDisk(1, diskLocations[8], ownerPlayer.startingSpell); }
                if (ownerPID == 4) { SpawnFloppyDisk(1, diskLocations[9], ownerPlayer.startingSpell); }
            }
        }

        if (gambaAnimator.GetBool("isActive") == false)
        {
            if (activeScene.name == "BetterShop")
            {
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
        //random spell
        if (name == "")
        {

        }
        else
        {
            GameObject disk = Instantiate(floppy, location, Quaternion.identity);
            SpellCode_FloppyDisk info = disk.GetComponent<SpellCode_FloppyDisk>();
            info.diskName = name;
            info.ownerPID = ownerPID;
            floppys.Add(disk);
            if (floppys.Count > 1)
            {
                Destroy(disk);
            }
        }
    }
}

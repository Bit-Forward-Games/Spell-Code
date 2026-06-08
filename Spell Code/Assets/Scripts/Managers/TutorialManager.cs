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

public class Tutorial : MonoBehaviour
{
    public Exit_Door door;
    public GambaMachine machine;
    private GameManager gM;

    public SpriteRenderer gambaHitGif;
    public SpriteRenderer floppyPickupGif;

    public TextMeshProUGUI gambaHitText;
    public TextMeshProUGUI floppyPickupText;
    public TextMeshProUGUI passiveDisplay;

    string bigStox;
    string demonX;
    string vWave;
    string killeez;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //machine.isActive = true;
        //machine2.isActive = true;
        gM = GameManager.Instance;
        floppyPickupGif.enabled = false;
        floppyPickupText.enabled = false;

        bigStox = "BigStox has Stock Stability<sprite name=\"StockStability\">";
        killeez = "Killeez has Reps<sprite name=\"Reps\">";
        demonX = "Demon-X has Demon Aura<sprite name=\"DemonAura\">";
        vWave = "VWave has Flow State<sprite name=\"FlowState\">";

        passiveDisplay.text = demonX + "\n" + bigStox + "\n" + killeez + "\n" + vWave;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //Machine 1
        //if (machine.isActive && machine.CheckHitboxCollision())
        //{
        //    Debug.Log("Hitbox collision detected!");
        //    Debug.Log("TUTORIAL GAMBA");
        //    machine.isActive = false;

        //    machine.SpawnFloppyDisk(machine.ownerPID, machine.tutorialLocs[0], "Skillshot Slash");
        //}

        if (machine.isActive)
        {
            gambaHitGif.enabled = true;
        }
        else if (machine.isActive == false)
        {
            gambaHitGif.enabled = false;
            gambaHitText.enabled = false;
            floppyPickupGif.enabled = true;
            floppyPickupText.enabled = true;
        }

        if (door.CheckAllPlayersReady()) { gM.sceneManager.LoadScene("MainMenu"); }
    }
}

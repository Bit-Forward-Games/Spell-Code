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
    public GambaMachine machine2;
    private GameManager gM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //machine.isActive = true;
        //machine2.isActive = true;
        gM = GameManager.Instance;
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

        if (door.CheckAllPlayersReady()) { gM.sceneManager.LoadScene("MainMenu"); }
    }
}

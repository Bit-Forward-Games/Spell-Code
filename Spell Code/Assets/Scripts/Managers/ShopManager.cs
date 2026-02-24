using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;

public class ShopManager : MonoBehaviour
{
    private GameManager gameManager;

    void Start()
    {
        gameManager = GameManager.Instance;

        foreach (GameObject gamba in gameManager.gambas)
        {
            gamba.GetComponent<GambaMachine>().activatedCount = 0;
            gamba.GetComponent<GambaMachine>().gambaAnimator.SetBool("isActive", true);
        }

        foreach (SpellCode_Gate gate in gameManager.gates) { gate.isOpen = false; }

    }
}
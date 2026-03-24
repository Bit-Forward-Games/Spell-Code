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
    private DataManager dataManager;


    private bool allPlayersChosen = false;
    private bool backToGameplay = false;

    public InputSnapshot[] inputSnapshots = new InputSnapshot[4];

    public Image p1_spellCard;
    public Image p2_spellCard;
    public Image p3_spellCard;
    public Image p4_spellCard;

    public List<string> spells;

    [SerializeField]
    private List<string> p1_choices;
    [SerializeField]
    private List<string> p2_choices;
    [SerializeField]
    private List<string> p3_choices;
    [SerializeField]
    private List<string> p4_choices;

    public List<string> GetP1Choices() => p1_choices;
    public List<string> GetP2Choices() => p2_choices;

    private int p1_index
    {
        get => gameManager.p1_shopIndex;
        set => gameManager.p1_shopIndex = value;
    }

    private int p2_index
    {
        get => gameManager.p2_shopIndex;
        set => gameManager.p2_shopIndex = value;
    }

    private int p3_index = 0;
    private int p4_index = 0;

    public TextMeshProUGUI p1_spellText;
    public TextMeshProUGUI p2_spellText;
    public TextMeshProUGUI p3_spellText;
    public TextMeshProUGUI p4_spellText;

    // Online shop state tracking
    private bool localPlayerReadyForGameplay = false;
    private bool remotePlayerReadyForGameplay = false;

    void Start()
    {
        gameManager = GameManager.Instance;
        dataManager = DataManager.Instance;

        foreach (GameObject gamba in gameManager.gambas)
        {
            gamba.GetComponent<GambaMachine>().activatedCount = 0;
            gamba.GetComponent<GambaMachine>().gambaAnimator.SetBool("isActive", true);
        }

        foreach (SpellCode_Gate gate in gameManager.gates) { gate.isOpen = false; }

    }
}
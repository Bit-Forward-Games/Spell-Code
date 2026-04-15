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
    public List<string> GetP3Choices() => p3_choices;
    public List<string> GetP4Choices() => p4_choices;

    public void SetChoicesForPlayer(int playerIndex, List<string> choices)
    {
        List<string> copy = choices != null ? new List<string>(choices) : new List<string>();
        switch (playerIndex)
        {
            case 0:
                p1_choices = copy;
                break;
            case 1:
                p2_choices = copy;
                break;
            case 2:
                p3_choices = copy;
                break;
            case 3:
                p4_choices = copy;
                break;
        }
    }

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

    private int p3_index
    {
        get => gameManager.p3_shopIndex;
        set => gameManager.p3_shopIndex = value;
    }

    private int p4_index
    {
        get => gameManager.p4_shopIndex;
        set => gameManager.p4_shopIndex = value;
    }

    public TextMeshProUGUI p1_spellText;
    public TextMeshProUGUI p2_spellText;
    public TextMeshProUGUI p3_spellText;
    public TextMeshProUGUI p4_spellText;

    void Start()
    {
        gameManager = GameManager.Instance;
        dataManager = DataManager.Instance;

        if (gameManager != null)
        {
            foreach (GameObject gamba in gameManager.gambas)
            {
                if (gamba == null) continue;

                GambaMachine gambaMachine = gamba.GetComponent<GambaMachine>();
                if (gambaMachine == null) continue;

                bool hasActiveOwner = gambaMachine.ownerPID > 0
                    && gambaMachine.ownerPID <= gameManager.playerCount
                    && gameManager.players[gambaMachine.ownerPID - 1] != null;
                int roundsPlayed = dataManager != null ? dataManager.totalRoundsPlayed : 0;
                bool ownerCanUseShop = hasActiveOwner
                    && gameManager.players[gambaMachine.ownerPID - 1].spellList != null
                    && gameManager.players[gambaMachine.ownerPID - 1].spellList.Count < roundsPlayed + 1;

                gambaMachine.activatedCount = ownerCanUseShop ? 0 : 3;
                gambaMachine.ownerPlayer = hasActiveOwner ? gameManager.players[gambaMachine.ownerPID - 1] : null;
                if (gambaMachine.gambaAnimator != null)
                {
                    gambaMachine.gambaAnimator.SetBool("isActive", ownerCanUseShop);
                }
            }
        }

        foreach (SpellCode_Gate gate in gameManager.gates) { gate.isOpen = false; }

    }
}

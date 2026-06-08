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
        switch (playerIndex)
        {
            case 0:
                CopyChoicesInto(ref p1_choices, choices);
                break;
            case 1:
                CopyChoicesInto(ref p2_choices, choices);
                break;
            case 2:
                CopyChoicesInto(ref p3_choices, choices);
                break;
            case 3:
                CopyChoicesInto(ref p4_choices, choices);
                break;
        }
    }

    private static void CopyChoicesInto(ref List<string> target, List<string> source)
    {
        if (target == null)
        {
            target = new List<string>(source != null ? source.Count : 0);
        }
        else
        {
            target.Clear();
        }

        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            target.Add(source[i]);
        }
    }

    // private int p1_index
    // {
    //     get => gameManager.p1_shopIndex;
    //     set => gameManager.p1_shopIndex = value;
    // }

    // private int p2_index
    // {
    //     get => gameManager.p2_shopIndex;
    //     set => gameManager.p2_shopIndex = value;
    // }

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
                PlayerController owner = hasActiveOwner ? gameManager.players[gambaMachine.ownerPID - 1] : null;
                bool ownerCanUseShop = hasActiveOwner
                    && owner.spellList != null
                    && (gameManager.isOnlineMatchActive
                        ? owner.spellList.Count < 6 && !owner.chosenSpell
                        : owner.spellList.Count < roundsPlayed + 1);

                gambaMachine.activatedCount = ownerCanUseShop ? 0 : 3;
                gambaMachine.ownerPlayer = owner;
                gambaMachine.isActive = ownerCanUseShop;
            }
        }

        foreach (SpellCode_Gate gate in gameManager.gates) { gate.isOpen = false; }

    }
}

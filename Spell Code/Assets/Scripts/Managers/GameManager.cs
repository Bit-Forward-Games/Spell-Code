using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;

    public bool isRunning;

    private void Awake()
    {
        // If an instance already exists and it's not this one, destroy this duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            // Otherwise, set this as the instance
            Instance = this;
            // Optional: Prevent the GameObject from being destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isRunning = true;
        StartCoroutine(End());
    }

    // Update is called once per frame
    void Update()
    {
    
    }

    private void FixedUpdate()
    {
        RunFrame();

        AnimationManager.Instance.RenderGameState();
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }
    }

    /// <summary>
    /// Runs a single frame of the game.
    /// </summary>
    protected  void RunFrame()
    {
        if (!isRunning)
            return;

        long[] inputs = new long[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        UpdateGameState(inputs);
        if (CheckGameEnd(GetActivePlayerControllers()))
        {
            //Game end logic here
            MatchEnd();
            isRunning = false;
        }
    }

    /// <summary>
    /// Updates the game state based on the provided inputs.
    /// </summary>
    /// <param name="inputs">Array of inputs for each player.</param>
    protected void UpdateGameState(long[] inputs)
    {
        ProjectileManager.Instance.UpdateProjectiles();
        HitboxManager.Instance.ProcessCollisions();
        for (int i = 0; i < playerCount; i++)
        {
            players[i].PlayerUpdate(inputs[i]);
        }
    }

    //gets called everytime a new player enters, recreates player array
    public void GetPlayerControllers( PlayerInput playerInput)
    {
        players[playerCount] = playerInput.GetComponent<PlayerController>();
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);
        playerCount++;



    }

    public bool CheckGameEnd(PlayerController[] playerControllers)
    {
        int alivePlayers = 0;
        foreach(PlayerController player in playerControllers)
        {
            if (player.currrentPlayerHealth > 0) alivePlayers++;
        }
        if (alivePlayers <= 1 && playerCount >1)
        {
            return true;
        }
        return false;
    }

    public void MatchEnd()
    {
        //general game data
        SaveDataHolder gameData = new SaveDataHolder();
        gameData.dateTime = System.DateTime.Now.ToString();

        //player data, looped for each player
        if (playerCount > 0)
        {
            gameData.playerData = new PlayerData[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                gameData.playerData[i] = new PlayerData();
                gameData.playerData[i].basicsFired = players[i].basicsFired;
                gameData.playerData[i].codesFired = players[i].spellsFired;
                gameData.playerData[i].codesMissed = players[i].spellsHit;
                gameData.playerData[i].synthesizer = players[i].characterName;
                gameData.playerData[i].times = players[i].times;

                gameData.playerData[i].spellList = new string[players[i].spellList.Length];
                for (int j = 0; j < players[i].spellList.Length; j++)
                {
                    if (players[i].spellList[j] is null)
                    {
                        gameData.playerData[i].spellList[j] = "no spell";
                    }
                    else
                    {
                        gameData.playerData[i].spellList[j] = players[i].spellList[j].spellName;
                   }
                }
            }
        }

        //save the data to file
        //if true, it will use remote save as well (which isn't a thing yet, so keep it false)
        SaveData saver = DataSaver.MakeSaver(false);
        StartCoroutine(saver.Save(gameData));
    }

    private IEnumerator End()
    {
        yield return new WaitForSeconds(3);
        MatchEnd();
    }
    public PlayerController[] GetActivePlayerControllers()
    {
        PlayerController[] activePlayers = new PlayerController[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            activePlayers[i] = players[i];
        }
        return activePlayers;
    }
}

using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour/*NonPersistantSingleton<GameManager>*/
{
    public static GameManager Instance { get; private set; }

    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;

    public bool isRunning;
    public bool isSaved;
    private DataManager dataManager;
    public TempSpellDisplay[] tempSpellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public StageDataSO currentStage;

    public int round = 1;
    public bool roundOver;

    public bool prevSceneWasShop;

    private void Awake()
    {
        // if an instance already exists and it's not this one, destroy this duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            // otherwise, set this as the instance
            Instance = this;
            // optional: prevent the gameobject from being destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isRunning = true;
        isSaved = false;

        dataManager = DataManager.Instance;
        //StartCoroutine(End());
    }

    // Update is called once per frame
    void Update()
    {
        // If current scene isn't the gameplay scene, ensure players are marked dead and the temp UI is disabled.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "TestGameplay")
        {
            // Set all known players to not alive
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null)
                    {
                        //players[i].isAlive = false;
                        players[i].gameObject.GetComponent<SpriteRenderer>().enabled = false;
                    }
                }
            }

            // Attempt to find and disable a child named "tempUI" (case-insensitive common variants)
            //TempUIScript tempUI = transform.Find("tempUI") ?? transform.Find("TempUI") ?? transform.Find("TempSpellUI") ?? transform.Find("TempSpellDisplay");
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(false);
            }

        }
        else
        {
                       // Ensure temp UI is enabled during gameplay
            if (tempUI != null)
            {
                tempUI.gameObject.SetActive(true);
            }
            // Also ensure all players' sprites are enabled
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].isAlive)
                    {
                        players[i].gameObject.GetComponent<SpriteRenderer>().enabled = true;
                    }
                }
            }
        }

        //This is just a shortcut for me to test stuff

        //if (Input.GetKeyDown(KeyCode.K))
        //{
        //    SaveMatch();
        //}

        //if ` is pressed, toggle box rendering
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }

        
    }

    private void FixedUpdate()
    {
        if (prevSceneWasShop)
        {
            ResetPlayers();
            prevSceneWasShop = false;
        }

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
    protected void RunFrame()
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
            for (int i = 0; i < playerCount; i++)
            {
                if (players[i].isAlive)
                {
                    Debug.Log("Player " + (i + 1) + " wins the match!");
                    players[i].isAlive = false; //reset for next round
                    break;
                }
            }
            dataManager.totalRoundsPlayed += 1;

            //Game end logic here
            if (dataManager.totalRoundsPlayed == 3)
            {
                dataManager.totalRoundsPlayed = 0;
                GameEnd();
            }
            else
            {
                RoundEnd();
            }
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

        //update each player update values
        for (int i = 0; i < playerCount; i++)
        {
            players[i].PlayerUpdate(inputs[i]);
        }

        for (int i = 0; i < playerCount; i++)
        {
            if (players[i].isAlive) {
                players[i].ProcEffectUpdate();
            }
        }
    }

    //gets called everytime a new player enters, recreates player array
    public void GetPlayerControllers(PlayerInput playerInput)
    {
        players[playerCount] = playerInput.GetComponent<PlayerController>();
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        AnimationManager.Instance.InitializePlayerVisuals(players[playerCount], playerCount);
        playerCount++;



    }

    public bool CheckGameEnd(PlayerController[] playerControllers)
    {
        int alivePlayers = 0;
        foreach (PlayerController player in playerControllers)
        {
            if (player.isAlive) alivePlayers++;
        }
        if (alivePlayers <= 1 && playerCount > 1)
        {
            return true;
        }
        return false;
    }

    public void ResetPlayers()
    {
        foreach (PlayerController player in players)
        {
            if (player != null)
            {
                player.SpawnPlayer(Vector2.zero);
            }
        }

        isSaved = false;
    }

    public void SaveMatch()
    {
        //general game data
        MatchData matchData = new MatchData();

        //player data, looped for each player
        if (playerCount > 0)
        {
            matchData.playerData = new PlayerData[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                float totalSpelltime = 0;

                //raw stats
                matchData.playerData[i] = new PlayerData();
                matchData.playerData[i].basicsFired = players[i].basicsFired;
                matchData.playerData[i].codesFired = players[i].spellsFired;
                matchData.playerData[i].codesHit = players[i].spellsHit;
                matchData.playerData[i].synthesizer = players[i].characterName;
                matchData.playerData[i].times = players[i].times;

                if (players[i].currrentPlayerHealth > 0)
                {
                    matchData.playerData[i].matchWon = true;
                }
                else
                {
                    matchData.playerData[i].matchWon = false;
                }

                //calculated accuracy
                if(players[i].basicsFired + players[i].spellsFired == 0)
                {
                    matchData.playerData[i].accuracy = 0;
                }
                else
                {
                    matchData.playerData[i].accuracy = players[i].spellsHit / (players[i].basicsFired + players[i].spellsFired);
                }

                //calculated avg time to cast a spell (totalTime / instances of times) 
                for (int k = 0; k < players[i].times.Count; k++)
                {
                    totalSpelltime += players[i].times[k];
                }

                if (players[i].times.Count > 0)
                {
                    matchData.playerData[i].avgTimeToCast = totalSpelltime / players[i].times.Count;
                }
                else
                {
                    matchData.playerData[i].avgTimeToCast = 0;
                }

                    //save spell name to spellList provided it isn't null. If null, add 'no spell'
                    matchData.playerData[i].spellList = new string[players[i].spellList.Count];
                for (int j = 0; j < players[i].spellList.Count; j++)
                {
                    if (players[i].spellList[j] is null)
                    {
                        matchData.playerData[i].spellList[j] = "no spell";
                    }
                    else
                    {
                        matchData.playerData[i].spellList[j] = players[i].spellList[j].spellName;
                    }
                }
            }
        }

        //save match data to gameData object
        dataManager.gameData.matchData.Add(matchData);

        //players = new PlayerController[4];
        //playerCount = 0;
    }

    //A round is 1 match + spell acquisition phase
    public void RoundEnd()
    {
        if (!isSaved)
        {
            SaveMatch();
            isSaved = true;
        }
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
        SceneManager.LoadScene("Shop");
    }

    //called when a game ends (game is a series of matches/rounds)
    public void GameEnd()
    {
        if (!isSaved)
        {
            SaveMatch();
            isSaved = true;
        }

        dataManager.SaveToFile();
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
        SceneManager.LoadScene("End");
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

    //resets the raw stats for each player back to 0 or their base state
    public void ResetPlayerStats()
    {
        for (int i = 0; i < playerCount; i++)
        {
            players[i].basicsFired = 0;
            players[i].spellsFired = 0;
            players[i].spellsHit = 0;
            players[i].times = new List<float>();

        }
    }
}

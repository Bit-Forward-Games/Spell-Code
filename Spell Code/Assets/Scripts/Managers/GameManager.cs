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
        //// If current scene isn't the gameplay scene, ensure players are marked dead and the temp UI is disabled.
        //Scene activeScene = SceneManager.GetActiveScene();
        //if (activeScene.name != "DEMO" && activeScene.name != "Gameplay")
        //{
        //    // Set all known players to not alive
        //    if (players != null)
        //    {
        //        for (int i = 0; i < players.Length; i++)
        //        {
        //            if (players[i] != null)
        //            {
        //                //players[i].isAlive = false;
        //                players[i].gameObject.GetComponent<SpriteRenderer>().enabled = false;
        //            }
        //        }
        //    }

        //    // Attempt to find and disable a child named "tempUI" (case-insensitive common variants)
        //    //TempUIScript tempUI = transform.Find("tempUI") ?? transform.Find("TempUI") ?? transform.Find("TempSpellUI") ?? transform.Find("TempSpellDisplay");
        //    if (tempUI != null)
        //    {
        //        tempUI.gameObject.SetActive(false);
        //    }

        //}
        //else
        //{
        //               // Ensure temp UI is enabled during gameplay
        //    if (tempUI != null)
        //    {
        //        tempUI.gameObject.SetActive(true);
        //    }
        //    // Also ensure all players' sprites are enabled
        //    if (players != null)
        //    {
        //        for (int i = 0; i < players.Length; i++)
        //        {
        //            if (players[i] != null && players[i].isAlive)
        //            {
        //                players[i].gameObject.GetComponent<SpriteRenderer>().enabled = true;
        //            }
        //        }
        //    }
        //}

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

        for (int i = 0; i < playerCount; i++)
        {
            players[i].playerNum.text = "P" + (i + 1);
        }

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

    //reset players after each round
    public void ResetPlayers()
    {
        for(int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].basicsFired = 0;
                players[i].spellsFired = 0;
                players[i].spellsHit = 0;
                players[i].times = new List<float>();
                players[i].SpawnPlayer(Vector2.zero);
                players[i].SpawnPlayer(currentStage.playerSpawnTransform[i]);
            }
        }

        isSaved = false;
    }

    /// <summary>
    /// Restart gamestate when "play" or "rematch" is pressed
    /// </summary>
    public void RestartGame()
    {
        dataManager.totalRoundsPlayed = 0;
        
       //reset each player to their starting values
       for (int i = 0; i < players.Length; i++)
       {
            if (players[i] != null)
            {
                //this is different from ResetPlayers()
                players[i].ResetPlayer();
                players[i].SpawnPlayer(currentStage.playerSpawnTransform[i]);
            }
       }
    }

    //A round is 1 match + spell acquisition phase
    public void RoundEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
            isSaved = true;
        }
        ProjectileManager.Instance.DeleteAllProjectiles();
        isRunning = false;
        SceneManager.LoadScene("Shop");
    }

    /// <summary>
    /// called when a game ends (game is a series of matches/rounds)
    /// </summary>
    public void GameEnd()
    {
        if (!isSaved)
        {
            dataManager.SaveMatch();
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
}

using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour/*NonPersistantSingleton<GameManager>*/
{
    public static GameManager Instance { get; private set; }

    public GameObject MainMenuScreen;

    public PlayerController[] players = new PlayerController[4];
    public int playerCount = 0;

    public bool isRunning;
    public bool isSaved;
    private DataManager dataManager;
    public TempSpellDisplay[] tempSpellDisplays = new TempSpellDisplay[4];
    public TempUIScript tempUI;
    public StageDataSO[] stages;
    public StageDataSO lobbySO;
    // public StageDataSO currentStage;
    public int currentStageIndex = 0;

    public List<GameObject> tempMapGOs = new List<GameObject>();
    public GameObject lobbyMapGO;

    [HideInInspector]
    public ShopManager shopManager;

    public GO_Door goDoorPrefab;

    public bool roundOver;
    public bool gameOver;

    public bool prevSceneWasShop;

    //game timers
    public float roundEndTimer = 0f;
    public int roundEndTransitionTime = 2;
    public TextMeshPro playerWinText;

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

        //goDoorPrefab = GetComponentInChildren<GO_Door>();

        SetStage(-1);
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
        //    // Ensure temp UI is enabled during gameplay
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

        //if ` is pressed, toggle box rendering
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            BoxRenderer.RenderBoxes = !BoxRenderer.RenderBoxes;
        }


    }

    private void FixedUpdate()
    {
        //if (prevSceneWasShop)
        //{
        //    ResetPlayers();
        //    prevSceneWasShop = false;
        //}

        //if the current scene is shop and shop manager is not assigned, assign it


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
        //get controller inputs for all players
        long[] inputs = new long[playerCount];
        for (int i = 0; i < inputs.Length; ++i)
        {
            inputs[i] = players[i].GetInputs();
        }

        Scene activeScene = SceneManager.GetActiveScene();

        ///shop specific update
        if (activeScene.name == "Shop")
        {
            if (shopManager == null)
            {
                shopManager = FindAnyObjectByType<ShopManager>();
            }
            shopManager.ShopUpdate(inputs);
        }
        else
        {
            shopManager = null;
        }


        //if the game is not running, skip the update (everything after this uses player controller updates)
        if (!isRunning)
            return;

       

        UpdateGameState(inputs);

        if(activeScene.name == "MainMenu")
        {
            goDoorPrefab.CheckOpenDoor();
            
            if(goDoorPrefab.CheckAllPlayersReady())
            {
                LoadRandomGameplayStage();
            }
        }
        else if (activeScene.name == "Gameplay")
        {
            if (CheckGameEnd(GetActivePlayerControllers()))
            {
                for (int i = 0; i < playerCount; i++)
                {
                    if (players[i].isAlive)
                    {
                        Debug.Log("Player " + (i + 1) + " wins the match!");
                        //playerWinText.text = "Player " + (i + 1) + " wins the match!";
                        players[i].isAlive = false; //reset for next round
                        players[i].roundsWon++;

                        if (players[i].roundsWon >= 3) { gameOver = true; }
                        break;
                    }
                }

                if (roundEndTransitionTime >= roundEndTimer) 
                { 
                    roundEndTimer += Time.deltaTime;
                }

                //Game end logic here
                if (roundEndTransitionTime <= roundEndTimer)
                {
                    ClearStages();
                    if (gameOver)
                    {
                        GameEnd();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                    else
                    {
                        RoundEnd();
                        Debug.Log(roundEndTimer);
                        roundEndTimer = 0;
                    }
                }
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
            if (players[i].isAlive)
            {
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
        Vector2[] spawnPos = GetSpawnPositions();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].basicsFired = 0;
                players[i].spellsFired = 0;
                players[i].spellsHit = 0;
                players[i].times = new List<float>();
                players[i].isAlive = true;
                players[i].SpawnPlayer(spawnPos[i]);
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
        Vector2[] spawnPositions = GetSpawnPositions();
        //reset each player to their starting values
        for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                {
                    //this is different from ResetPlayers()


                    players[i].ResetPlayer();
                players[i].SpawnPlayer(spawnPositions[i]);
                }
            }
    }

    public Vector2[] GetSpawnPositions()
    {
        if (currentStageIndex < 0)
        {
            return new Vector2[] {
                lobbySO.playerSpawnTransform[0],
                lobbySO.playerSpawnTransform[1],
                lobbySO.playerSpawnTransform[2],
                lobbySO.playerSpawnTransform[3]};
        }
        else
        {
            return new Vector2[] {
                stages[currentStageIndex].playerSpawnTransform[0],
                stages[currentStageIndex].playerSpawnTransform[1],
                stages[currentStageIndex].playerSpawnTransform[2],
                stages[currentStageIndex].playerSpawnTransform[3]};
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

    public void SetStage(int stageIndex)
    {
        currentStageIndex = stageIndex;

        ClearStages();
        //enable the temp map gameobject corresponding to the stage index, disable others
        if (currentStageIndex == -1)
        {
            lobbyMapGO.SetActive(true);
            return;
        }
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            if (i == stageIndex)
            {
                tempMapGOs[i].SetActive(true);
            }
        }
    }

    public void LoadRandomGameplayStage()
    {
        //make the next stage random but different from the last stage
        int newStageIndex;
        do
        {
            newStageIndex = Random.Range(0, stages.Length);

        } while (currentStageIndex == newStageIndex);
        SetStage(newStageIndex);
        SceneManager.LoadScene("Gameplay");
        ResetPlayers();
    }

    public void ClearStages()
    {
        for (int i = 0; i < tempMapGOs.Count; i++)
        {
            tempMapGOs[i].SetActive(false);
        }
        lobbyMapGO.SetActive(false);
    }


    public void SetMenuActive(bool isActive)
    {
        if (MainMenuScreen != null)
        {
            MainMenuScreen.SetActive(isActive);
        }
    }
}

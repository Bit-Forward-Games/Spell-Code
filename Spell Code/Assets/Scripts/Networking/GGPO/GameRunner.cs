//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using Unity.Collections;
//using UnityEngine;
//using UnityGGPO;

///// <summary>
///// Abstract class representing the game runner responsible for managing game state and player inputs.
///// </summary>
//public abstract class GameRunner : MonoBehaviour
//{
//    protected int[] playerHandles;
//    protected int framesAhead = 0;
//    protected GGPOPlayer[] players;
//    [SerializeField] protected PlayerController[] playerCons;

//    //getter for player controllers
//    //public PlayerController[] PlayerCons { get => playerCons; }

//    //method to set the player controllers
//    public void SetPlayerControllers()
//    {
//        //already in the scene
//        PlayerController[] pC = FindObjectsByType<PlayerController>(FindObjectsSortMode.None); //faster than FindObjectsOfType
//        playerCons = pC;
//        //refer to game session manager instance
//    }

//    public void SetPlayerControllers(PlayerController[] pC)
//    {
//        playerCons = pC;
//        //refer to game session manager instance
//    }

//    protected int currentFrame = 0;
//    protected bool isRunning = false;
//    protected double start;
//    protected double next;
//    protected string message;

//    /// <summary>
//    /// Start is called before the first frame update.
//    /// </summary>
//    void Start()
//    {
//        Debug.developerConsoleVisible = true;
//    }

//    /// <summary>
//    /// Initializes the game runner with the specified players and local player index.
//    /// </summary>
//    /// <param name="players">Array of GGPOPlayer objects representing the players.</param>
//    /// <param name="localPlayer">Index of the local player.</param>
//    public abstract void Init(GGPOPlayer[] players = null, int localPlayer = 0);

//    /// <summary>
//    /// Initializes the game runner with the specified local player index.
//    /// </summary>
//    /// <param name="localPlayer">Index of the local player.</param>
//    public void Init(int localPlayer = 0)
//    {
//        Init(null, localPlayer);
//    }

//    /// <summary>
//    /// Updates the game state. Must be implemented by derived classes.
//    /// </summary>
//    protected abstract void Update();

//    /// <summary>
//    /// Runs a single frame of the game. Must be implemented by derived classes.
//    /// </summary>
//    protected abstract void RunFrame();

//    /// <summary>
//    /// Renders the GUI elements.
//    /// </summary>
//    //protected void OnGUI()
//    //{
//    //    GUILayout.TextField(message);
//    //}

//    /// <summary>
//    /// Converts milliseconds to frames.
//    /// </summary>
//    /// <param name="time">Time in milliseconds.</param>
//    /// <returns>Equivalent time in frames.</returns>
//    protected double MsToFrame(double time)
//    {
//        return time / 1000.0 * 60.0;
//    }

//    /// <summary>
//    /// Converts frames to milliseconds.
//    /// </summary>
//    /// <param name="ms">Time in frames.</param>
//    /// <returns>Equivalent time in milliseconds.</returns>
//    protected double FrameToMs(double ms)
//    {
//        return ms * 1000.0 / 60.0;
//    }

//    /// <summary>
//    /// Updates the game state based on the provided inputs.
//    /// </summary>
//    /// <param name="inputs">Array of inputs for each player.</param>
//    protected void UpdateGameState(long[] inputs)
//    {
//        HitboxManager.Instance.ProcessCollisions();
//        for (int i = 0; i < playerCons.Length; i++)
//        {
//            playerCons[i].PlayerUpdate(inputs[i]);
//        }
//    }

//    /// <summary>
//    /// Renders the current game state.
//    /// </summary>
//    public void RenderGameState()
//    {
//        //the order of the players swaps every frame so we need direct access to the player controllers through a field on manager to avoid issues
//        AnimationManager.Instance.RenderGameState();
//        ProjectileManager.Instance.UpdateInRunner();
//    }

    

//    public void RenderGameStateNetwork()
//    {
//        AnimationManager.Instance.RenderGameStateNetwork();
//        //ProjectileManager.Instance.UpdateInRunner();
//    }


//}
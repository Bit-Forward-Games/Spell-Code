using NUnit.Framework;
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
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        RunFrame();

        AnimationManager.Instance.RenderGameState();

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
    }

    /// <summary>
    /// Updates the game state based on the provided inputs.
    /// </summary>
    /// <param name="inputs">Array of inputs for each player.</param>
    protected void UpdateGameState(long[] inputs)
    {
        //HitboxManager.Instance.ProcessCollisions();
        for (int i = 0; i < playerCount; i++)
        {
            players[i].PlayerUpdate(inputs[i]);
        }
    }

    public void GetPlayerControllers( PlayerInput playerInput)
    {
        //players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        players[playerCount] = playerInput.GetComponent<PlayerController>();
        players[playerCount].inputs.AssignInputDevice(playerInput.devices[0]);
        playerCount++;
        
    }

}

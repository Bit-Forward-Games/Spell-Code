//using SharedGame;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using Unity.Collections;
//using UnityEngine;
//using UnityGGPO;

//public class LocalRunner : GameRunner
//{
//    /// <summary>
//    /// Initializes the LocalRunner with the specified players and local player index.
//    /// </summary>
//    /// <param name="players">Array of GGPOPlayer objects representing the players.</param>
//    /// <param name="localPlayer">Index of the local player.</param>
//    public override void Init(GGPOPlayer[] players = null, int localPlayer = 0)
//    {
//        if (players == null) // create a default player setup
//        {
//            players = new GGPOPlayer[2];

//            players[0].player_num = 1;
//            players[0].port = 0;
//            players[0].ip_address = "";
//            players[0].type = GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL;

//            players[1].player_num = 2;
//            players[1].port = 0;
//            players[1].ip_address = "";
//            players[1].type = GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL;
//        }
//        StartSession("local", players);

//    }

//    /// <summary>
//    /// Starts a new session with the specified name and players.
//    /// </summary>
//    /// <param name="name">Name of the session.</param>
//    /// <param name="ggpoPlayers">Array of GGPOPlayer objects representing the players.</param>
//    public void StartSession(string name, GGPOPlayer[] ggpoPlayers)
//    {
//        players = ggpoPlayers;
//        playerCons[0].position = new Vector3(-80, -40, 0);
//        playerCons[1].position = new Vector3(80, -40, 0);
//        playerCons[0].opponent = playerCons[1];
//        playerCons[1].opponent = playerCons[0];

//        //create an initial port state
//        int port = 0;

//        playerHandles = new int[playerCount];

//        next = Utils.TimeGetTime();

//        for (int i = 0; i < playerCount; i++)
//        {
//            if (players[i].type == GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL)
//            {
//                port = players[i].port;
//                players[i].port = 0;
//                players[i].ip_address = "";
//            }
//        }

//        isRunning = true;
//    }

//    /// <summary>
//    /// Updates the LocalRunner state.
//    /// </summary>
//    protected override void Update()
//    {
//        UpdateLocal();
//    }

//    /// <summary>
//    /// Runs a single frame of the game.
//    /// </summary>
//    protected override void RunFrame()
//    {
//        if (!isRunning)
//            return;

//        long[] inputs = new long[playerCons.Length];
//        for (int i = 0; i < inputs.Length; ++i)
//        {
//            inputs[i] = playerCons[i].GetInputs();
//        }

//        UpdateGameState(inputs);
//    }

//    /// <summary>
//    /// Cleans up resources when the LocalRunner is destroyed.
//    /// </summary>
//    private void OnDestroy()
//    {
//        if (isRunning)
//        {
//            Debug.Log("OnDestroy: closing GGPO session");
//            GGPO.Session.CloseSession();
//            isRunning = false;
//        }
//    }

//    private void OnApplicationQuit()
//    {
//        if (isRunning)
//        {
//            Debug.Log("OnApplicationQuit: closing GGPO session on application close");
//            GGPO.Session.CloseSession();
//            isRunning = false;
//        }

//        // only actually exit the process in a standalone build, never in the Editor
//#if !UNITY_EDITOR
//        Environment.Exit(0);
//#endif
//    }


//    /// <summary>
//    /// Updates the local state of the LocalRunner.
//    /// </summary>
//    private void UpdateLocal()
//    {
//        int now = (int)Utils.TimeGetTime(); // Ensure 'now' is an int
//        int frameTimeMs = (int)FrameToMs(1); // Ensure 'FrameToMs(1)' is an int
//        int nextInt = (int)next; // Ensure 'next' is an int

//        int deltaTimeMs = now - (nextInt - frameTimeMs); // Ensure consistent int operations
//        int frameCount = Mathf.Clamp(deltaTimeMs / frameTimeMs, 0, 5); // Ensure division result is int

//        int extraMs = Mathf.Max(0, nextInt - now - 1); // Ensure explicit cast for extraMs
//        GGPO.Session.Idle(extraMs);

//        if (now >= nextInt)
//        {
//            bool renderedThisFrame = false;

//            RunFrame(); // Run the logic for this frame

//            for (int i = 0; i < frameCount; i++)
//            {

//                if (!renderedThisFrame)
//                {
//                    RenderGameState();  // Call RenderGameState only once per frame
//                    renderedThisFrame = true; // Mark that we've rendered this frame
//                }
//            }

//            next = now + frameTimeMs; // Reset next frame time (next remains double)
//        }
//    }



//}
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using Unity.Collections;
//using UnityEngine;
//using UnityGGPO;

///// <summary>
///// GGPORunner class handles the initialization and running of GGPO sessions.
///// </summary>
//public class GGPORunner : GameRunner
//{
//    /// <summary>
//    /// Initializes the GGPO session with the provided players and local player index.
//    /// </summary>
//    /// <param name="players">Array of GGPOPlayer objects.</param>
//    /// <param name="localPlayer">Index of the local player.</param>
//    public override void Init(GGPOPlayer[] players = null, int localPlayer = 0)
//    {
//        if (players == null) // create a default player setup
//        {
//            players = new GGPOPlayer[2];

//            players[0].player_num = 1;
//            players[0].port = 7000;
//            players[0].ip_address = FCUtils.GetLocalIPAddress();
//            players[0].type = localPlayer == 0 ? GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL : GGPOPlayerType.GGPO_PLAYERTYPE_REMOTE;

//            players[1].player_num = 2;
//            players[1].port = 7001;
//            players[1].ip_address = FCUtils.GetLocalIPAddress();
//            players[1].type = localPlayer == 1 ? GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL : GGPOPlayerType.GGPO_PLAYERTYPE_REMOTE;
//        }
//        StartSession("FatalCounter", players);
//    }

//    /// <summary>
//    /// Starts a GGPO session with the given name and players.
//    /// </summary>
//    /// <param name="name">Name of the session.</param>
//    /// <param name="ggpoPlayers">Array of GGPOPlayer objects.</param>
//    public void StartSession(string name, GGPOPlayer[] ggpoPlayers)
//    {
//        players = ggpoPlayers;
//        playerCons[0].position = new Vector3(-10, 0, 0);
//        playerCons[1].position = new Vector3(10, 0, 0);
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

//        Debug.Log(port);

//        int result = GGPO.Session.StartSession(OnBeginGame, OnAdvanceFrame,
//            OnLoadGameState, OnLogGameState,
//            OnSaveGameState, OnFreeBuffer,
//            OnConnectedToPeer, OnSynchronizingWithPeer,
//            OnSynchronizedWithPeer, OnRunning,
//            OnConnectionInterrupted, OnConnectionResumed,
//            OnDisconnectedFromPeer, OnTimeSync,
//            name, playerCount, port);

//        Debug.Log("Result of Starting Session: " + GGPO.GetErrorCodeMessage(result));

//        GGPO.Session.SetDisconnectTimeout(3000);
//        GGPO.Session.SetDisconnectNotifyStart(1000);

//        for (int i = 0; i < playerCount; i++)
//        {
//            result = GGPO.Session.AddPlayer(players[i], out playerHandles[i]);
//            Debug.Log("Result of player " + i + ": " + GGPO.GetErrorCodeMessage(result));

//            if (players[i].type == GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL)
//            {
//                GGPO.Session.SetFrameDelay(playerHandles[i], 2);
//            }
//        }

//        isRunning = true;
//    }

//    /// <summary>
//    /// Updates the GGPO session.
//    /// </summary>
//    protected override void Update()
//    {
//        UpdateFixedFastForward();
//    }

//    /// <summary>
//    /// Runs a single frame of the GGPO session.
//    /// </summary>
//    protected override void RunFrame()
//    {
//        if (!isRunning)
//            return;

//        int result = GGPO.OK;

//        long input;

//        for (int i = 0; i < playerCons.Length; i++)
//        {
//            if (players[i].type == GGPOPlayerType.GGPO_PLAYERTYPE_LOCAL)
//            {
//                input = playerCons[i].GetInputs();
//                result = GGPO.Session.AddLocalInput(playerHandles[i], input);
//            }
//        }

//        if (GGPO.SUCCEEDED(result))
//        {
//            try
//            {
//                long[] inputs = GGPO.Session.SynchronizeInput(playerCons.Length, out int disconnectedplayers);
//                UpdateGameState(inputs);

//                GGPO.Session.AdvanceFrame();
//            }
//            catch (Exception e)
//            {
//                Debug.Log("Error: " + e);
//            }
//        }

//        message = GGPO.GetErrorCodeMessage(result);
//    }

//    /// <summary>
//    /// Closes the GGPO session when the object is destroyed.
//    /// </summary>
//    private void OnDestroy()
//    {
//        GGPO.Session.CloseSession();
//    }

//    /// <summary>
//    /// Updates the GGPO session with fixed fast forward.
//    /// </summary>
//    private void UpdateFixedFastForward()
//    {
//        int now = Utils.TimeGetTime();
//        int extraMs = Mathf.Max(0, (int)(next - now - 1));
//        GGPO.Session.Idle(extraMs);

//        if (now >= next)
//        {
//            bool renderedThisFrame = false; // Prevent multiple render calls

//            do
//            {
//                RunFrame(); // Runs game logic and updates GGPO-synced animation state

//                if (!renderedThisFrame)
//                {

//                    RenderGameStateNetwork(); // Only render once per loop
//                    renderedThisFrame = true;
//                }

//                next += FrameToMs(1); // Move to next frame
//                if (framesAhead > 0)
//                {
//                    next += FrameToMs(1);
//                    --framesAhead;
//                }

//            } while (Utils.TimeGetTime() >= next); // Process multiple logic frames if needed
//        }
//    }


//    private void OnGUI()
//    {
//        GUILayout.TextField(message);
//    }

//    // ===== | Delegates | =====

//    /// <summary>
//    /// Delegate for beginning the game.
//    /// </summary>
//    /// <param name="text">Text parameter.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnBeginGame(string text) { return true; }

//    /// <summary>
//    /// Delegate for advancing the frame.
//    /// </summary>
//    /// <param name="flags">Flags parameter.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnAdvanceFrame(int flags)
//    {
//        long[] inputs = GGPO.Session.SynchronizeInput(2, out int disconnectedplayers);
//        playerCons[0].PlayerUpdate(inputs[0]);
//        playerCons[1].PlayerUpdate(inputs[1]);
//        GGPO.Session.AdvanceFrame();
//        return true;
//    }

//    /// <summary>
//    /// Delegate for loading the game state.
//    /// </summary>
//    /// <param name="data">Serialized game state data.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnLoadGameState(NativeArray<byte> data)
//    {
//        using (MemoryStream memoryStream = new MemoryStream(data.ToArray()))
//        {
//            using (BinaryReader reader = new BinaryReader(memoryStream))
//            {
//                //deserialize the game state
//                for (int i = 0; i < playerCons.Length; i++)
//                {
//                    playerCons[i].Deserialize(reader);
//                }
//            }
//        }

//        return true;
//    }

//    /// <summary>
//    /// Delegate for logging the game state.
//    /// </summary>
//    /// <param name="fileName">File name for the log.</param>
//    /// <param name="data">Serialized game state data.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnLogGameState(string fileName, NativeArray<byte> data) { return true; }

//    /// <summary>
//    /// Delegate for saving the game state.
//    /// </summary>
//    /// <param name="data">Serialized game state data.</param>
//    /// <param name="checksum">Checksum of the game state.</param>
//    /// <param name="frame">Current frame number.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnSaveGameState(out NativeArray<byte> data, out int checksum, int frame)
//    {
//        using (MemoryStream memoryStream = new MemoryStream())
//        {
//            using (BinaryWriter writer = new BinaryWriter(memoryStream))
//            {
//                //serialize the game state
//                for (int i = 0; i < playerCons.Length; i++)
//                {
//                    playerCons[i].Serialize(writer);
//                }
//            }
//            data = new NativeArray<byte>(memoryStream.ToArray(), Allocator.Persistent);
//        }

//        // Calculate the checksum (e.g., Fletcher32)
//        checksum = Utils.CalcFletcher32(data);

//        return true;
//    }

//    /// <summary>
//    /// Delegate for freeing the buffer.
//    /// </summary>
//    /// <param name="data">Serialized game state data.</param>
//    private void OnFreeBuffer(NativeArray<byte> data)
//    {
//        if (data.IsCreated)
//        {
//            data.Dispose();
//        }
//    }

//    /// <summary>
//    /// Delegate for handling connection to a peer.
//    /// </summary>
//    /// <param name="connected_player">Index of the connected player.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnConnectedToPeer(int connected_player) { return true; }

//    /// <summary>
//    /// Delegate for synchronizing with a peer.
//    /// </summary>
//    /// <param name="synchronizing_player">Index of the synchronizing player.</param>
//    /// <param name="synchronizing_count">Current synchronization count.</param>
//    /// <param name="synchronizing_total">Total synchronization count.</param>
//    /// <returns>True if successful.</returns>
//    public bool OnSynchronizingWithPeer(int synchronizing_player, int synchronizing_count, int synchronizing_total)
//    {
//        Debug.Log($"Synchronizing with player {synchronizing_player}");
//        return true;
//    }

//    /// <summary>
//    /// Delegate for handling synchronization completion with a peer.
//    /// </summary>
//    /// <param name="synchronized_player">Index of the synchronized player.</param>
//    /// <returns>True if successful.</returns>
//    public bool OnSynchronizedWithPeer(int synchronized_player)
//    {
//        Debug.Log($"Synchronized with player {synchronized_player}");
//        return true;
//    }

//    /// <summary>
//    /// Delegate for handling the running state.
//    /// </summary>
//    /// <returns>True if successful.</returns>
//    public bool OnRunning()
//    {
//        return true;
//    }

//    /// <summary>
//    /// Delegate for handling connection interruption.
//    /// </summary>
//    /// <param name="connection_interrupted_player">Index of the interrupted player.</param>
//    /// <param name="connection_interrupted_disconnect_timeout">Disconnect timeout value.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnConnectionInterrupted(int connection_interrupted_player, int connection_interrupted_disconnect_timeout) { return true; }

//    /// <summary>
//    /// Delegate for handling connection resumption.
//    /// </summary>
//    /// <param name="connection_resumed_player">Index of the resumed player.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnConnectionResumed(int connection_resumed_player) { return true; }

//    /// <summary>
//    /// Delegate for handling disconnection from a peer.
//    /// </summary>
//    /// <param name="disconnected_player">Index of the disconnected player.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnDisconnectedFromPeer(int disconnected_player) { return true; }

//    /// <summary>
//    /// Delegate for handling time synchronization.
//    /// </summary>
//    /// <param name="timesync_frames_ahead">Number of frames ahead for time synchronization.</param>
//    /// <returns>True if successful.</returns>
//    private bool OnTimeSync(int timesync_frames_ahead)
//    {
//        framesAhead = timesync_frames_ahead;
//        return true;
//    }
//}
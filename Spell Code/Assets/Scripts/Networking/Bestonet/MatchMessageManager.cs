using System;
using System.IO;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using BestoNet.Collections;


public class MatchMessageManager : MonoBehaviour
{
    public static MatchMessageManager Instance { get; private set; }

    [Header("Network Settings")]
    [SerializeField] private int MATCH_MESSAGE_CHANNEL = 0;
    [SerializeField] private P2PSend INPUT_SEND_TYPE = P2PSend.UnreliableNoDelay;
    [SerializeField] private P2PSend ACK_SEND_TYPE = P2PSend.Reliable;
    private const byte PACKET_TYPE_READY = 2;
    private const byte PACKET_TYPE_MATCH_START = 3;
    private const byte PACKET_TYPE_LOBBY_READY = 10; // For lobby->gameplay transition
    private const byte PACKET_TYPE_SHOP_READY = 11;

    [Header("Ping Calculation")]
    public CircularArray<float> sentFrameTimes = new CircularArray<float>(RollbackManager.InputArraySize);
    public int Ping { get; private set; } = 100;

    // Make opponent ID accessible
    private SteamId opponentSteamId;
    public SteamId GetOpponentSteamId() => opponentSteamId;

    private bool isRunning = false;
    private bool localReadySent = false;
    private bool remoteReadyReceived = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
    }

    private void OnEnable()
    {
        SteamNetworking.OnP2PSessionRequest = OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed = OnP2PConnectionFailed;
    }

    private void OnDisable()
    {
        SteamNetworking.OnP2PSessionRequest = null;
        SteamNetworking.OnP2PConnectionFailed = null;
    }

    private void OnP2PSessionRequest(SteamId steamId)
    {
        Debug.Log($"P2P Session request from {steamId}");

        if (steamId == opponentSteamId || opponentSteamId == default)
        {
            Debug.Log($"Accepting P2P session from {steamId}");

            if (opponentSteamId == default)
            {
                opponentSteamId = steamId;
            }
        }
        else
        {
            Debug.LogWarning($"Rejecting P2P session from unknown user {steamId}");
        }
    }

    private void OnP2PConnectionFailed(SteamId steamId, P2PSessionError error)
    {
        Debug.LogError($"P2P Connection failed with {steamId}: {error}");
    }

    void Update()
    {
        if (!isRunning || !SteamClient.IsValid)
        {
            return;
        }

        if (SteamNetworking.OnP2PSessionRequest == null)
        {
            Debug.LogWarning("OnP2PSessionRequest callback is NULL!");
        }

        while (SteamNetworking.IsP2PPacketAvailable(MATCH_MESSAGE_CHANNEL))
        {
            P2Packet? packet = SteamNetworking.ReadP2PPacket(MATCH_MESSAGE_CHANNEL);
            if (packet.HasValue && packet.Value.SteamId == opponentSteamId)
            {
                try
                {
                    ProcessPacket(packet.Value.Data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing packet: {e}");
                }
            }
            else
            {
                Debug.LogWarning($"Received packet from unknown SteamId: {packet.Value.SteamId}");
            }
        }
    }

    public void SendReadySignal()
    {
        if (!opponentSteamId.IsValid || !isRunning)
        {
            Debug.LogWarning("Cannot send ready signal - not connected");
            return;
        }

        if (localReadySent)
        {
            Debug.Log("Ready signal already sent - skipping");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_READY);
                    writer.Write(SteamClient.SteamId.Value);

                    byte[] data = memoryStream.ToArray();

                    bool success = SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        data.Length,
                        MATCH_MESSAGE_CHANNEL,
                        P2PSend.Reliable
                    );

                    if (success)
                    {
                        localReadySent = true;
                        Debug.Log($"Sent READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ready signal: {e}");
        }
    }

    public void SendMatchStartConfirm()
    {
        if (!opponentSteamId.IsValid || !isRunning)
            return;

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_MATCH_START);

                    byte[] data = memoryStream.ToArray();

                    SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        data.Length,
                        MATCH_MESSAGE_CHANNEL,
                        P2PSend.Reliable
                    );

                    Debug.Log("Sent MATCH START confirmation");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending match start: {e}");
        }
    }

    // Send lobby ready for gameplay signal
    public void SendLobbyReadySignal()
    {
        if (!opponentSteamId.IsValid || !isRunning)
        {
            Debug.LogWarning("Cannot send lobby ready signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_LOBBY_READY);

                    byte[] data = memoryStream.ToArray();

                    bool success = SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        data.Length,
                        MATCH_MESSAGE_CHANNEL,
                        P2PSend.Reliable
                    );

                    if (success)
                    {
                        Debug.Log($"Sent LOBBY_READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send LOBBY_READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending lobby ready signal: {e}");
        }
    }

    // Send shop ready for gameplay signal
    public void SendShopReadySignal()
    {
        if (!opponentSteamId.IsValid || !isRunning)
        {
            Debug.LogWarning("Cannot send shop ready signal - not connected");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write(PACKET_TYPE_SHOP_READY);

                    byte[] data = memoryStream.ToArray();

                    bool success = SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        data.Length,
                        MATCH_MESSAGE_CHANNEL,
                        P2PSend.Reliable
                    );

                    if (success)
                    {
                        Debug.Log($"Sent SHOP_READY signal to {opponentSteamId}");
                    }
                    else
                    {
                        Debug.LogError("Failed to send SHOP_READY signal");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending shop ready signal: {e}");
        }
    }

    public void StartMatch(SteamId opponentId)
    {
        Debug.Log($"StartMatch called with opponent: {opponentId}");

        if (opponentId.IsValid)
        {
            this.opponentSteamId = opponentId;
            this.isRunning = true;
            Ping = 100;
            sentFrameTimes.Clear();

            ResetReadyFlags();
            SteamNetworking.AllowP2PPacketRelay(true);

            Debug.Log($"MatchMessageManager started. Opponent: {opponentSteamId}");

            SendHandshake();
        }
        else
        {
            Debug.LogError("MatchMessageManager: Invalid opponent SteamId provided.");
            this.isRunning = false;
        }
    }

    private void SendHandshake()
    {
        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)0xFF);
                    writer.Write("HANDSHAKE");

                    byte[] data = memoryStream.ToArray();

                    bool success = SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        data.Length,
                        MATCH_MESSAGE_CHANNEL,
                        P2PSend.Reliable
                    );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending handshake: {e}");
        }
    }

    public void StopMatch()
    {
        this.isRunning = false;
        this.opponentSteamId = default;
        Debug.Log("MatchMessageManager stopped.");
    }

    private void ProcessPacket(byte[] messageData)
    {
        if (RollbackManager.Instance == null)
        {
            Debug.LogWarning("RollbackManager is null, cannot process packet");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPacketReceived();
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream(messageData))
            {
                using (BinaryReader reader = new BinaryReader(memoryStream))
                {
                    byte packetType = reader.ReadByte();

                    // Handle handshake
                    if (packetType == 0xFF)
                    {
                        string message = reader.ReadString();
                        Debug.Log($"Received handshake: {message}");
                        SendHandshake();
                        return;
                    }

                    // Handle ready signal
                    if (packetType == PACKET_TYPE_READY)
                    {
                        ulong senderSteamId = reader.ReadUInt64();
                        Debug.Log($"Received READY signal from {senderSteamId}");

                        if (remoteReadyReceived)
                        {
                            Debug.Log("Remote ready already processed - skipping");
                            return;
                        }

                        remoteReadyReceived = true;

                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.OnOpponentReady();
                        }

                        if (!localReadySent)
                        {
                            SendReadySignal();
                        }

                        return;
                    }

                    // Handle match start confirmation
                    if (packetType == PACKET_TYPE_MATCH_START)
                    {
                        Debug.Log("Received MATCH START confirmation");
                        return;
                    }

                    // Handle lobby ready for gameplay
                    if (packetType == PACKET_TYPE_LOBBY_READY)
                    {
                        Debug.Log("Received LOBBY_READY signal from opponent");
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.OnOpponentReadyForGameplay();
                        }
                        return;
                    }

                    // Handle shop ready for gameplay
                    if (packetType == PACKET_TYPE_SHOP_READY)
                    {
                        Debug.Log("Received SHOP_READY signal from opponent");
                        if (GameManager.Instance != null && GameManager.Instance.shopManager != null)
                        {
                            GameManager.Instance.shopManager.OnOpponentReadyForGameplay();
                        }
                        return;
                    }

                    // Handle input packets
                    if (packetType == 0)
                    {
                        if (GameManager.Instance != null && GameManager.Instance.isWaitingForOpponent)
                        {
                            Debug.LogWarning("Received input packet while still in lobby - ignoring");
                            return;
                        }

                        int remoteFrameAdvantage = reader.ReadInt32();
                        int startFrame = reader.ReadInt32();
                        int inputCount = reader.ReadByte();

                        Debug.Log($"Received Input Packet: StartFrame={startFrame}, Count={inputCount}");

                        for (int i = 0; i < inputCount; i++)
                        {
                            int frame = startFrame + i;
                            ulong input = reader.ReadUInt64();

                            bool isNewInput = !RollbackManager.Instance.receivedInputs.ContainsKey(frame);

                            // ALWAYS update received input with latest data
                            RollbackManager.Instance.SetOpponentInput(frame, input);

                            // Only send ACK for genuinely new frames
                            if (isNewInput)
                            {
                                SendMessageACK(frame);
                            }

                            if (i == inputCount - 1)
                            {
                                RollbackManager.Instance.SetRemoteFrameAdvantage(frame, remoteFrameAdvantage);
                                RollbackManager.Instance.SetRemoteFrame(frame);
                                Debug.Log($"Updated remoteFrame to {frame}");
                            }
                        }
                    }
                    else if (packetType == 1) // ACK
                    {
                        int ackFrame = reader.ReadInt32();
                        ProcessACK(ackFrame);
                    }
                    else
                    {
                        Debug.LogWarning($"Received unknown packet type: {packetType}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Packet processing error: {e}");
        }
    }

    public void ResetReadyFlags()
    {
        localReadySent = false;
        remoteReadyReceived = false;
    }

    private void ProcessACK(int frame)
    {
        float sentTime = sentFrameTimes.Get(frame);

        if (sentTime > 0f)
        {
            int rttMs = Mathf.RoundToInt((Time.unscaledTime - sentTime) * 1000f);
            Ping = (int)Mathf.Lerp(Ping, rttMs, 0.1f);
            sentFrameTimes.Insert(frame, 0f);
        }
    }

    public void SendInputs()
    {
        if (RollbackManager.Instance == null || !opponentSteamId.IsValid || !isRunning)
        {
            Debug.LogError($"SendInputs BLOCKED: RBM={RollbackManager.Instance != null}, " +
                      $"OpponentValid={opponentSteamId.IsValid}, isRunning={isRunning}");
            return;
        }

        int currentLocalFrame = GameManager.Instance.frameNumber;
        int latestTargetFrame = currentLocalFrame + RollbackManager.Instance.InputDelay;
        int firstFrameToSend = Math.Max(0, latestTargetFrame - RollbackManager.Instance.MaxRollBackFrames - RollbackManager.Instance.InputDelay);

        int inputCount = latestTargetFrame - firstFrameToSend + 1;
        if (inputCount <= 0) return;

        const int MaxInputsPerPacket = 15;
        if (inputCount > MaxInputsPerPacket)
        {
            firstFrameToSend = latestTargetFrame - MaxInputsPerPacket + 1;
            inputCount = MaxInputsPerPacket;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)0);
                    writer.Write(RollbackManager.Instance.localFrameAdvantage);
                    writer.Write(firstFrameToSend);
                    writer.Write((byte)inputCount);

                    sentFrameTimes.Insert(latestTargetFrame, Time.unscaledTime);

                    for (int i = 0; i < inputCount; i++)
                    {
                        int frame = firstFrameToSend + i;
                        ulong inputToSend = RollbackManager.Instance.clientInputs.ContainsKey(frame)
                                            ? RollbackManager.Instance.clientInputs.GetInput(frame)
                                            : 0UL;
                        writer.Write(inputToSend);
                    }

                    byte[] data = memoryStream.ToArray();
                    int dataSize = data.Length;

                    bool success = SteamNetworking.SendP2PPacket(
                                opponentSteamId,
                                data,
                                dataSize,
                                MATCH_MESSAGE_CHANNEL,
                                INPUT_SEND_TYPE
                            );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending inputs: {e}");
        }
    }

    public void SendMessageACK(int frameToAck)
    {
        if (!opponentSteamId.IsValid || !isRunning)
        {
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(memoryStream))
                {
                    writer.Write((byte)1);
                    writer.Write(frameToAck);

                    byte[] data = memoryStream.ToArray();
                    int dataSize = data.Length;
                    bool success = SteamNetworking.SendP2PPacket(
                        opponentSteamId,
                        data,
                        dataSize,
                        MATCH_MESSAGE_CHANNEL,
                        ACK_SEND_TYPE
                    );
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending ACK: {e}");
        }
    }
}
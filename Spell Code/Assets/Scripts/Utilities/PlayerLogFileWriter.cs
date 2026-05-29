using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlayerLogFileWriter : MonoBehaviour
{
    private const int MaxBufferedLines = 2000;

    private static readonly object LogLock = new object();
    private static readonly List<string> PendingLines = new List<string>();
    private static PlayerLogFileWriter instance;
    private static bool subscribed;

    private StreamWriter writer;
    private int activeSlot = -1;
    private string activePath;
    // Track which paths we've already truncated this process lifetime so that re-inits
    // (writer became null, slot flickered briefly, etc.) append to the existing file
    // instead of wiping it. Without this, any mid-session writer recreation silently
    // truncates the session's log and we lose all data prior to the re-init.
    private static readonly HashSet<string> TruncatedPathsThisSession = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!subscribed)
        {
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            subscribed = true;
        }

        if (instance != null)
        {
            return;
        }

        GameObject loggerObject = new GameObject("PlayerLogFileWriter");
        DontDestroyOnLoad(loggerObject);
        instance = loggerObject.AddComponent<PlayerLogFileWriter>();
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        string line = FormatLogLine(condition, stackTrace, type);
        lock (LogLock)
        {
            PendingLines.Add(line);
            if (PendingLines.Count > MaxBufferedLines)
            {
                PendingLines.RemoveAt(0);
            }
        }
    }

    private static string FormatLogLine(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log || string.IsNullOrWhiteSpace(stackTrace))
        {
            return condition;
        }

        return condition + Environment.NewLine + stackTrace;
    }

    private void Update()
    {
        int desiredSlot = TryGetOnlineLocalSlot();
        if (desiredSlot >= 0)
        {
            EnsureWriter(desiredSlot);
        }

        FlushPendingLines();
    }

    private void OnApplicationQuit()
    {
        if (writer == null)
        {
            EnsureWriter(Mathf.Max(0, activeSlot));
        }

        FlushPendingLines();
        CloseWriter();
    }

    private void OnDestroy()
    {
        CloseWriter();
        if (instance == this)
        {
            instance = null;
        }
    }

    private int TryGetOnlineLocalSlot()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.isOnlineMatchActive)
        {
            return -1;
        }

        return Mathf.Clamp(gameManager.localPlayerIndex, 0, 3);
    }

    private void EnsureWriter(int slot)
    {
        if (writer != null && activeSlot == slot)
        {
            return;
        }

        CloseWriter();

        activeSlot = slot;
        activePath = Path.Combine(GetPlayerLogsDirectory(), GetLogFileName(slot));
        Directory.CreateDirectory(Path.GetDirectoryName(activePath));

        // Truncate this file the FIRST time we open it this process lifetime, but APPEND
        // on any subsequent re-open. This guards against mid-session writer re-creations
        // (slot flicker, etc.) silently wiping the session's log. We still get a fresh
        // file at the start of each game run because TruncatedPathsThisSession is per-process.
        bool firstOpen = TruncatedPathsThisSession.Add(activePath);
        FileMode mode = firstOpen ? FileMode.Create : FileMode.Append;

        writer = new StreamWriter(new FileStream(activePath, mode, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };

        // Marker line so a re-init shows up clearly in the log. First open is also marked,
        // which is harmless and helps confirm the writer is healthy.
        writer.WriteLine($"=== PlayerLogFileWriter open slot={slot} mode={(firstOpen ? "create" : "append")} time={DateTime.UtcNow:HH:mm:ss.fff}Z ===");
    }

    private void FlushPendingLines()
    {
        if (writer == null)
        {
            return;
        }

        List<string> linesToWrite = null;
        lock (LogLock)
        {
            if (PendingLines.Count > 0)
            {
                linesToWrite = new List<string>(PendingLines);
                PendingLines.Clear();
            }
        }

        if (linesToWrite == null)
        {
            return;
        }

        for (int i = 0; i < linesToWrite.Count; i++)
        {
            writer.WriteLine(linesToWrite[i]);
        }
    }

    private void CloseWriter()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Dispose();
        writer = null;
    }

    private static string GetLogFileName(int slot)
    {
        return slot <= 0 ? "player.log" : $"player({slot}).log";
    }

    private static string GetPlayerLogsDirectory()
    {
        string root = Application.isEditor
            ? FindWorkspaceRoot(Application.dataPath)
            : FindBuildRoot(Application.dataPath);

        return Path.Combine(root, "Player logs");
    }

    private static string FindBuildRoot(string dataPath)
    {
        DirectoryInfo dataDirectory = new DirectoryInfo(dataPath);
        return dataDirectory.Parent != null
            ? dataDirectory.Parent.FullName
            : Directory.GetCurrentDirectory();
    }

    private static string FindWorkspaceRoot(string startPath)
    {
        DirectoryInfo directory = new DirectoryInfo(startPath);
        for (int i = 0; i < 8 && directory != null; i++)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "Spell Code")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using BestoNet.Types;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

//this script is purely so that I can keep a persistent storage of data
//throughout the entire game, I can't have the shell deleting itself every time
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    private GameManager gM;

    public SaveDataHolder gameData = new SaveDataHolder();

    public int totalRoundsPlayed = 0;
    public float roundTimer;
    void Awake()
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

            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Clear the stale reference when the persistent copy is torn down (ExecuteOrder66), so
        // callers' `Instance != null` checks — and especially `Instance?.` which bypasses Unity's
        // destroyed-object null — don't invoke methods on a dead object.
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        gM = GameManager.Instance;

        gameData.dateTime = System.DateTime.Now.ToString();
        gameData.matchData = new List<MatchData>();

        if (gM == null)
        {
            return;
        }

        for (ushort i = 0; i < gM.tempMapGOs.Count; i++)
        {
            if (!gameData.arenaData.deathDict.ContainsKey(gM.tempMapGOs[i].name))
            {
                gameData.arenaData.deathDict.Add(gM.tempMapGOs[i].name, new List<Vector2>());
            }
            if (!gameData.arenaData.hitDict.ContainsKey(gM.tempMapGOs[i].name))
            {
                gameData.arenaData.hitDict.Add(gM.tempMapGOs[i].name, new List<Vector2>());
            }
        }
    }

    //temp for testing in-engine
    // private void FixedUpdate()
    // {
    //     while (GameManager.Instance != null && GameManager.Instance.sceneManager.sceneName == "Gameplay") 
    //     {

    //     }
    //     //This is just a shortcut for me to test stuff

    //     //if (Input.GetKeyDown(KeyCode.L))
    //     //{
    //         //save the data to file
    //         //if true, it will use remote save as well (which isn't a thing yet, so keep it false)
    //      //   SaveData saver = DataSaver.MakeSaver(false);
    //      //   StartCoroutine(saver.Save(gameData));

    //      //   Debug.Log("Data Saved");
    //     //}
    // }


    //function to save data to file
    public void SaveToFile()
    {
        //save the data to file
        //if true, it will use remote save as well (which isn't a thing yet, so keep it false)
        SaveData saver = DataSaver.MakeSaver(false);
        StartCoroutine(saver.Save(gameData));

        Debug.Log("Data Saved");
    }

    public void SaveMatch()
    {
        if (gM == null)
        {
            gM = GameManager.Instance;
        }

        //general game data
        MatchData matchData = new MatchData();

        matchData.matchNum = (byte)(totalRoundsPlayed);
        matchData.matchLength = roundTimer / 60;

        // playerCount is the serialized slot span for sparse online rosters. Save real roster
        // participants (including a peer who later disconnected), but not fabricated gap objects.
        PlayerController[] matchPlayers = gM.GetMatchParticipantControllers();
        if (matchPlayers.Length > 0)
        {
            matchData.playerData = new PlayerData[matchPlayers.Length];

            for (int i = 0; i < matchPlayers.Length; i++)
            {
                PlayerController player = matchPlayers[i];
                Fixed totalSpelltime = Fixed.FromInt(0);

                //raw stats
                matchData.playerData[i] = new PlayerData();
                matchData.playerData[i].pID = player.pID;
                matchData.playerData[i].basicsFired = player.basicsFired;
                matchData.playerData[i].codesFired = player.spellsFired;
                matchData.playerData[i].codesHit = player.spellsHit;
                matchData.playerData[i].synthesizer = player.characterName;
                matchData.playerData[i].times = player.times;

                if (player.currentPlayerHealth > 0)
                {
                    matchData.playerData[i].matchWon = true;
                }
                else
                {
                    matchData.playerData[i].matchWon = false;
                }

                //calculated accuracy
                if (player.basicsFired > 0 && player.spellsFired > 0)
                {
                    matchData.playerData[i].accuracy = player.spellsHit / (player.basicsFired + player.spellsFired);
                }               
                if (player.basicsFired == 0 || player.spellsFired == 0)
                {
                    matchData.playerData[i].accuracy = 0f;
                }

                //calculated avg time to cast a spell (totalTime / instances of times) 
                for (int k = 0; k < player.times.Count; k++)
                {
                    totalSpelltime += player.times[k];
                }

                if (player.times.Count > 0)
                {
                    Fixed playerTimesCount = Fixed.FromInt(player.times.Count);
                    matchData.playerData[i].avgTimeToCast = totalSpelltime / playerTimesCount;
                }
                else
                {
                    matchData.playerData[i].avgTimeToCast = Fixed.FromInt(0);
                }

                //save spell name to spellList provided it isn't null. If null, add 'no spell'
                matchData.playerData[i].spellList = new string[player.spellList.Count];

                for (int j = 0; j < player.spellList.Count; j++)
                {
                    if (player.spellList[j] is null)
                    {
                        matchData.playerData[i].spellList[j] = "no spell";
                    }
                    else
                    {
                        matchData.playerData[i].spellList[j] = player.spellList[j].spellName;
                    }
                }
            }
        }

        //save match data to gameData object
        gameData.matchData.Add(matchData);
    }


    /// <summary>
    /// Readies the data saver for the next game
    /// </summary>
    public void ResetData()
    {
        gameData = new SaveDataHolder();
        gameData.dateTime = System.DateTime.Now.ToString();
        gameData.matchData = new List<MatchData>();

        gameData.arenaData = new ArenaData();
        for (ushort i = 0; i < GameManager.Instance.tempMapGOs.Count; i++)
        {
            if (!gameData.arenaData.deathDict.ContainsKey(GameManager.Instance.tempMapGOs[i].name))
            {
                gameData.arenaData.deathDict.Add(GameManager.Instance.tempMapGOs[i].name, new List<Vector2>());
            }
            if (!gameData.arenaData.hitDict.ContainsKey(GameManager.Instance.tempMapGOs[i].name))
            {
                gameData.arenaData.hitDict.Add(GameManager.Instance.tempMapGOs[i].name, new List<Vector2>());
            }
        }

        totalRoundsPlayed = 0;
    }
}

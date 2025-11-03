using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

//this script is purely so that I can keep a persistent storage of data
//throughout the entire game, I can't have the shell deleting itself every time
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    private GameManager gM;

    public SaveDataHolder gameData = new SaveDataHolder();
    

    public int totalRoundsPlayed = 0;
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

    private void Start()
    {
        gameData.dateTime = System.DateTime.Now.ToString();
        gameData.matchData = new List<MatchData>();

        gM = GameManager.Instance;
    }

    //temp for testing in-engine
    private void Update()
    {
        //This is just a shortcut for me to test stuff

        //if (Input.GetKeyDown(KeyCode.L))
        //{
            //save the data to file
            //if true, it will use remote save as well (which isn't a thing yet, so keep it false)
         //   SaveData saver = DataSaver.MakeSaver(false);
         //   StartCoroutine(saver.Save(gameData));

         //   Debug.Log("Data Saved");
        //}
    }


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

        //player data, looped for each player
        if (gM.playerCount > 0)
        {
            matchData.playerData = new PlayerData[gM.playerCount];

            for (int i = 0; i < gM.playerCount; i++)
            {
                float totalSpelltime = 0;

                //raw stats
                matchData.playerData[i] = new PlayerData();
                matchData.playerData[i].basicsFired = gM.players[i].basicsFired;
                matchData.playerData[i].codesFired = gM.players[i].spellsFired;
                matchData.playerData[i].codesHit = gM.players[i].spellsHit;
                matchData.playerData[i].synthesizer = gM.players[i].characterName;
                matchData.playerData[i].times = gM.players[i].times;

                if (gM.players[i].currrentPlayerHealth > 0)
                {
                    matchData.playerData[i].matchWon = true;
                }
                else
                {
                    matchData.playerData[i].matchWon = false;
                }

                //calculated accuracy
                if (gM.players[i].basicsFired + gM.players[i].spellsFired > 0)
                {
                    matchData.playerData[i].accuracy = gM.players[i].spellsHit / (gM.players[i].basicsFired + gM.players[i].spellsFired);
                }
                else
                {
                    matchData.playerData[i].accuracy = 0f;
                }

                //calculated avg time to cast a spell (totalTime / instances of times) 
                for (int k = 0; k < gM.players[i].times.Count; k++)
                {
                    totalSpelltime += gM.players[i].times[k];
                }

                if (gM.players[i].times.Count > 0)
                {
                    matchData.playerData[i].avgTimeToCast = totalSpelltime / gM.players[i].times.Count;
                }
                else
                {
                    matchData.playerData[i].avgTimeToCast = 0;
                }

                //save spell name to spellList provided it isn't null. If null, add 'no spell'
                matchData.playerData[i].spellList = new string[gM.players[i].spellList.Count];

                for (int j = 0; j < gM.players[i].spellList.Count; j++)
                {
                    if (gM.players[i].spellList[j] is null)
                    {
                        matchData.playerData[i].spellList[j] = "no spell";
                    }
                    else
                    {
                        matchData.playerData[i].spellList[j] = gM.players[i].spellList[j].spellName;
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
    }
}

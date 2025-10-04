using UnityEngine;
using System.Collections.Generic;

//this script is purely so that I can keep a persistent storage of data
//throughout the entire game, I can't have the shell deleting itself every time
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    public SaveDataHolder gameData = new SaveDataHolder();

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
        gameData.matchData = new List<MatchData>();
    }

    //temp for testing in-engine
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            //save the data to file
            //if true, it will use remote save as well (which isn't a thing yet, so keep it false)
            SaveData saver = DataSaver.MakeSaver(false);
            StartCoroutine(saver.Save(gameData));

            Debug.Log("Data Saved");
        }
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
}

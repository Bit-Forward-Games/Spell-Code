////using UnityEngine;

////public class GameManager : MonoBehaviour
////{
////    //create private instance
////    private static GameManager _instance;

////    //public reference
////    public static GameManager Instance
////    {
////        get
////        {
////            if (_instance is null)
////                Debug.LogError("Game Manager is NULL");
////            return _instance;
////        }
////    }

////    // Start is called once before the first execution of Update after the MonoBehaviour is created
////    void Start()
////    {
////        //initialize GM
////        _instance = this;
////        DontDestroyOnLoad(_instance);
////    }

////    // Update is called once per frame
////    void Update()
////    {
        
//    }

//    //called at end of a match
//    public void MatchEnd()
//    {
//        //general game data

//        //player data, looped for each player
//        PlayerData playerData = new PlayerData();
//        playerData.codesFired = 1;
//        playerData.codesMissed = 1;
//        playerData.synthesizer = "sluh";

//        //save the data to file
//        SaveData saver = DataSaver.MakeSaver(false);
//        StartCoroutine(saver.Save(playerData));
//    }

//}

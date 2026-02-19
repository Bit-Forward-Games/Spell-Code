using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using BestoNet.Types;

using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using FixedVec3 = BestoNet.Types.Vector3<BestoNet.Types.Fixed32>;
using System.Linq;


public class StageDataBuilder : MonoBehaviour
{
    public StageDataSO stageDataSO;

    private GameObject[] platforms;
    private GameObject[] solids;
    private GameObject[] playerSpawns;
    private GameObject[] activatableSolids;
    
    void Start()
    {
        platforms = GameObject.FindGameObjectsWithTag("Platform");
        solids = GameObject.FindGameObjectsWithTag("Solid");

        playerSpawns = new GameObject[4];
        playerSpawns[0] = GameObject.FindGameObjectWithTag("Player Spawn");
        playerSpawns[1] = GameObject.FindGameObjectWithTag("Player 2 Spawn ");
        playerSpawns[2] = GameObject.FindGameObjectWithTag("Player 3 Spawn");
        playerSpawns[3] = GameObject.FindGameObjectWithTag("Player 4 Spawn");
        //playerSpawns = GameObject.FindGameObjectsWithTag("Player Spawn");
        activatableSolids = GameObject.FindGameObjectsWithTag("activatableSolid");


        stageDataSO = ScriptableObject.CreateInstance<StageDataSO>();

        stageDataSO.platformCenter = new Vector2[platforms.Length];
        stageDataSO.platformExtent = new Vector2[platforms.Length];

        stageDataSO.solidCenter = new Vector2[solids.Length];
        stageDataSO.solidExtent = new Vector2[solids.Length];

        stageDataSO.playerSpawnTransform = new Vector3[playerSpawns.Length];

        stageDataSO.activatableSolidCenter = new Vector3[activatableSolids.Length];
        stageDataSO.activatableSolidExtent = new Vector3[activatableSolids.Length];

    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKey("s") && Input.GetKeyDown("o"))
        {
            GetStageData();
            #if UNITY_EDITOR
            SaveStageData();
            #endif
        }
    }

#endif

    void GetStageData()
    {
        int i = 0;
        int j = 0; 
        //int k = 0;
        int l = 0;
        foreach (GameObject platform in platforms)
        {
            Bounds platformColliderBounds = platform.GetComponent<BoxCollider2D>().bounds;
            stageDataSO.platformCenter[i] = platformColliderBounds.center;
            stageDataSO.platformExtent[i] = platformColliderBounds.extents;
            i++;
        }

        foreach (GameObject solid in solids)
        {
            Bounds solidColliderBounds = solid.GetComponent<BoxCollider2D>().bounds;
            stageDataSO.solidCenter[j] = solidColliderBounds.center;
            stageDataSO.solidExtent[j] = solidColliderBounds.extents;
            j++;
        }

        //foreach (GameObject spawn in playerSpawns)
        //{
        //    Transform spawnTransforms = spawn.GetComponent<Transform>();
        //    stageDataSO.playerSpawnTransform[k] = spawnTransforms.position;
        //    k++;
        //}

        //if there are not exactly 4 player spawn points,...
        //if(playerSpawns.Length != 4)
        //{
        //    //log a warning
        //    Debug.LogWarning(gameObject.name + " tried to create a Stage Data Scriptable Object with a scene that does not contain exactly 4 player spawn points!. Add exactly 4 objects with the \"Player Spawn\" tag.");

        //    //return
        //    return;
        //}

        //add each player spawn point to playerSpawnTransform based on their tags
        stageDataSO.playerSpawnTransform[0] = playerSpawns[0].transform.position;
        stageDataSO.playerSpawnTransform[1] = playerSpawns[1].transform.position;
        stageDataSO.playerSpawnTransform[2] = playerSpawns[2].transform.position;
        stageDataSO.playerSpawnTransform[3] = playerSpawns[3].transform.position;

        foreach (GameObject activatableSolid in activatableSolids)
        {
            Bounds activatableSolidColliderBounds = activatableSolid.GetComponent<BoxCollider>().bounds;
            stageDataSO.activatableSolidCenter[l] = activatableSolidColliderBounds.center;
            stageDataSO.activatableSolidExtent[l] = activatableSolidColliderBounds.extents;
            l++;
        }
    }

    #if UNITY_EDITOR
    void SaveStageData()
    {
        // Specify the path where you want to save it
        // Must start with "Assets/" and include file extension
        string directory = "Assets/SO";
        
        // Make sure the directory exists
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    
        // Create the path with scene name + "StageDataSO"
        string path = $"{directory}/{sceneName}StageDataSO.asset";
        
        // Create the asset at the specified path
        AssetDatabase.CreateAsset(stageDataSO, path);
        
        // Save changes
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //log a message
        Debug.Log(sceneName + "StageDataSO.asset has been saved to: " + directory);
    }
    #endif
}

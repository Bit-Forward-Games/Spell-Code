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
    public BorderType _borderType = BorderType.Collision;
    public StageType _stageType = StageType.General;
    public StageSize _stageSize = StageSize.Medium;

    private GameObject[] platforms;
    private GameObject[] solids;
    private GameObject[] playerSpawns;
    private GameObject[] npcSpawns;
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
        npcSpawns = GameObject.FindGameObjectsWithTag("NPC Spawn");
        activatableSolids = GameObject.FindGameObjectsWithTag("activatableSolid");


        stageDataSO = ScriptableObject.CreateInstance<StageDataSO>();

        stageDataSO.platformCenter = new Vector2[platforms.Length];
        stageDataSO.platformExtent = new Vector2[platforms.Length];

        stageDataSO.solidCenter = new Vector2[solids.Length];
        stageDataSO.solidExtent = new Vector2[solids.Length];

        stageDataSO.playerSpawnTransform = new Vector2[playerSpawns.Length];
        stageDataSO.npcSpawnTransform = new Vector2[npcSpawns.Length];

        stageDataSO.activatableSolidCenter = new Vector3[activatableSolids.Length];
        stageDataSO.activatableSolidExtent = new Vector3[activatableSolids.Length];

    }

#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKey("s") && Input.GetKeyDown("o"))
        {
            GetStageData();
            SaveStageData();
        }
    }
#endif

    void GetStageData()
    {
        int i = 0;
        int j = 0; 
        int k = 0;
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

        foreach (GameObject spawn in npcSpawns)
        {
           Transform spawnTransforms = spawn.GetComponent<Transform>();
           stageDataSO.npcSpawnTransform[k] = new Vector2(spawnTransforms.position.x, spawnTransforms.position.y);
           k++;
        }

        //if there are not exactly 4 player spawn points,...
        //if(playerSpawns.Length != 4)
        //{
        //    //log a warning
        //    Debug.LogWarning(gameObject.name + " tried to create a Stage Data Scriptable Object with a scene that does not contain exactly 4 player spawn points!. Add exactly 4 objects with the \"Player Spawn\" tag.");

        //    //return
        //    return;
        //}

        //add each player spawn point to playerSpawnTransform based on their tags
        stageDataSO.playerSpawnTransform[0] = new Vector2(playerSpawns[0].transform.position.x, playerSpawns[0].transform.position.y);
        stageDataSO.playerSpawnTransform[1] = new Vector2(playerSpawns[1].transform.position.x, playerSpawns[1].transform.position.y);
        stageDataSO.playerSpawnTransform[2] = new Vector2(playerSpawns[2].transform.position.x, playerSpawns[2].transform.position.y);
        stageDataSO.playerSpawnTransform[3] = new Vector2(playerSpawns[3].transform.position.x, playerSpawns[3].transform.position.y);

        foreach (GameObject activatableSolid in activatableSolids)
        {
            Bounds activatableSolidColliderBounds = activatableSolid.GetComponent<BoxCollider>().bounds;
            stageDataSO.activatableSolidCenter[l] = activatableSolidColliderBounds.center;
            stageDataSO.activatableSolidExtent[l] = activatableSolidColliderBounds.extents;
            l++;
        }

        //apply the inspector set variables _borderType and _stageType
        stageDataSO.borderType = _borderType;
        stageDataSO.stageType = _stageType;

        //set stage borders based on _stageSize
        switch (_stageSize)
        {
            case StageSize.Small:
                //if this is suppose to be a looping stage,...
                if (_borderType == BorderType.Loop)
                {
                    //set borders to a super small size
                    stageDataSO.borderMin = new Vector3(-240f, -200f, 0f);
                    stageDataSO.borderMax = new Vector3(240, 200, 0f);

                    //set the cameraBorderMin and cameraBorderMax
                    stageDataSO.camBorderMin = new Vector3(-300, -130f, 0f);
                    stageDataSO.camBorderMax = new Vector3(300f, 130f, 0f);
                }
                //else this stage does not loop,...
                else
                {
                    //set borders to defualt small size
                    stageDataSO.borderMin = new Vector3(-300f, -200f, 0f);
                    stageDataSO.borderMax = new Vector3(300, 300, 0f);
                }

                //break
                break;

            case StageSize.Medium:
                //set borders to defualt medium size
                stageDataSO.borderMin = new Vector3(-300f, -220f, 0f);
                stageDataSO.borderMax = new Vector3(300, 220, 0f);

                //if this is suppose to be a looping stage, set the cameraBorderMin and cameraBorderMax
                if (_borderType == BorderType.Loop)
                {
                    stageDataSO.camBorderMin = new Vector3(-300, -205f, 0f);
                    stageDataSO.camBorderMax = new Vector3(300f, 205f, 0f);
                }
                
                //break
                break;

            case StageSize.Large:
                //set borders to defualt large size
                stageDataSO.borderMin = new Vector3(-654f, -520f, 0f);
                stageDataSO.borderMax = new Vector3(654, 520, 0f);

                //break
                break;

            case StageSize.Custom:
                //get previous SOs values


                //set border and camera mins and maxs to the previous SOs values
                stageDataSO.borderMin = Vector3.zero;
                stageDataSO.borderMax = Vector3.zero;

                //break
                break;
        }

        //set dynamic camera toggle
        stageDataSO.dynamicCamera = (_borderType != BorderType.Loop) ? true : false;
    }

#if UNITY_EDITOR
    void SaveStageData()
    {
        // Specify the path where you want to save it
        // Must start with "Assets/" and include file extension
        string directory = "Assets/SO/Arena SOs";
        
        // Make sure the directory exists
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        //string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string sceneName = this.gameObject.name;

        // Create the path with scene name + "_StageDataSO"
        string path = $"{directory}/{sceneName} StageDataSO.asset";
        
        // Create the asset at the specified path
        AssetDatabase.CreateAsset(stageDataSO, path);
        
        // Save changes
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        //log a message
        Debug.Log(sceneName + " StageDataSO.asset has been saved to: " + directory);
    }
#endif
}

using UnityEngine;

public class StageDataBuilder : MonoBehaviour
{
    public StageDataSO stageDataSO;

    private GameObject[] platforms;
    private GameObject[] solids;
    
    void Start()
    {
        platforms = GameObject.FindGameObjectsWithTag("Platform");
        solids = GameObject.FindGameObjectsWithTag("Solid");

        stageDataSO.platformCenter = new Vector2[platforms.Length];
        stageDataSO.platformExtent = new Vector2[platforms.Length];

        stageDataSO.solidCenter = new Vector2[solids.Length];
        stageDataSO.solidExtent = new Vector2[solids.Length];

        GetStageData();
    }

    void GetStageData()
    {
        int i = 0;
        int j = 0; 
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
    }
}

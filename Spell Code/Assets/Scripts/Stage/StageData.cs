using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageData : MonoBehaviour
{
    private static StageData _instance;

    public int floorYval = -40;
    public int ceilingYval = 100;
    public int leftWallXval = -240;
    public int rightWallXval = 240;
    public Vector2Int p1pos = new(-30, -40);
    public Vector2Int p2pos = new(30, -40);

    public static StageData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<StageData>(); //replace deprecated method
                if (_instance == null)
                {
                    GameObject singletonObject = new();
                    _instance = singletonObject.AddComponent<StageData>();
                    singletonObject.name = typeof(StageData).ToString() + " (Singleton)";
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}

using UnityEngine;

public class GameManager : MonoBehaviour
{
    //create private instance
    private static GameManager _instance;

    //public reference
    public static GameManager Instance
    {
        get
        {
            if (_instance is null)
                Debug.LogError("Game Manager is NULL");
            return _instance;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //initialize GM
        _instance = this;
        DontDestroyOnLoad(_instance);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

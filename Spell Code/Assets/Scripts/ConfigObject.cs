//using System;
//using UnityEngine;
//using UnityEngine.AddressableAssets;
//using UnityEngine.SceneManagement;
//using UnityEngine.ResourceManagement.AsyncOperations;
//using UnityEngine.InputSystem;

//[CreateAssetMenu(fileName = "ConfigObject", menuName = "ScriptableObjects/SceneIndependentManager")]
//public class ConfigObject : ScriptableObjectSingleton<ConfigObject>
//{
//    //public static event Action OnHam;

//    public InputDevice playerOneDevice = null;
//    public InputDevice playerTwoDevice = null;
//    public string playerOneCharacter = "";
//    public string playerTwoCharacter = "";
//    public Texture2D[] playerOneTexture = null;
//    public Texture2D[] playerTwoTexture = null;
//    public InputActionMap playerOneActionMap = null;
//    public InputActionMap playerTwoActionMap = null;
//    public bool developerMode = true;
//    public Sprite stageBackground = null;

//    [SerializeField]
//    private AssetReferenceT<GameObject> prefab;
//    private GameObject instanceRef;
//    private static bool isInitialized = false;

//    public GameObject GetInstanceRef() => instanceRef;

//    public void InstantiatePrefab(Vector3 position, Transform parent = null)
//    {
//        if (prefab.RuntimeKeyIsValid())
//        {
//            prefab.InstantiateAsync(position, Quaternion.identity, parent).Completed += OnPrefabInstantiated;
//        }
//        else
//        {
//            Debug.LogError("Invalid prefab reference.");
//        }
//    }

//    private void OnPrefabInstantiated(AsyncOperationHandle<GameObject> obj)
//    {
//        if (obj.Status == AsyncOperationStatus.Succeeded)
//        {
//            instanceRef = obj.Result;
//            //OnHam?.Invoke();
//        }
//        else
//        {
//            Debug.LogError($"Failed to instantiate prefab: {obj.OperationException}");
//        }
//    }

//    public void ReleasePrefab()
//    {
//        if (instanceRef != null)
//        {
//            Addressables.ReleaseInstance(instanceRef);
//            instanceRef = null;
//            Debug.Log("Prefab instance released.");
//        }
//        else
//        {
//            Debug.LogWarning("No prefab instance to release.");
//        }
//    }

//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//    private static void Init()
//    {
//        if (isInitialized) return;
//        isInitialized = true;
//        LoadInstanceAsync();
//        OnSingletonReady += OnConfigObjectReady;
//    }

//    //public static void InvokeEventsTest()
//    //{
//    //    OnHam?.Invoke();
//    //    Debug.Log("OnHam invoked at time: " + Time.time);
//    //}

//    private static void OnConfigObjectReady(ConfigObject configInstance)
//    {
//        //Debug.Log("ConfigObject is ready.");
//        //OnHam += ConfigObject_OnHam;
//        SceneManager.sceneLoaded += SceneManager_sceneLoaded;
//    }

//    //private static void ConfigObject_OnHam()
//    //{
//    //    Debug.Log("OnHam event invoked in scene: " + SceneManager.GetActiveScene().name);
//    //}

//    private static void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
//    {
//        Debug.Log("Scene loaded: " + scene.name);
//    }
//}

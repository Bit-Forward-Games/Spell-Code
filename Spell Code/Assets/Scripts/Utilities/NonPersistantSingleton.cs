using UnityEngine;

[DisallowMultipleComponent] //editor prevention code below is rutime prevention
public class NonPersistantSingleton<T> : MonoBehaviour where T : Component
{
    private static T instance;
    public static bool HasInstance => instance != null;

    public static T TryGetInstance() => HasInstance ? instance : null;
    // Resilient instance getter
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<T>(); 
                if (instance == null)
                {
                    GameObject g = new GameObject(typeof(T).Name + " (Auto-Generated)");
                    instance = g.AddComponent<T>();
                }
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        InitSingleton();
    }

    protected virtual void InitSingleton()
    {
        if (!Application.isPlaying) return;
        instance = this as T;
    }
}

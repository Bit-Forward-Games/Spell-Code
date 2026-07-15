using System;
using UnityEngine;

public class DisplayChangeDetector : MonoBehaviour
{
    public static event Action OnDisplayChanged;

    private int lastWidth;
    private int lastHeight;

    void Start()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        OnDisplayChanged += () => GameManager.Instance.SetResolution();
    }

    void Update()
    {
        // Detect if the resolution has altered
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            // Trigger your custom event
            OnDisplayChanged?.Invoke();
        }
    }
}
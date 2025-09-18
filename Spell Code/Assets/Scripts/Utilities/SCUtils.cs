using System;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public static class SCUtils
{
    public static string GetLocalIPAddress()
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
    //ham

    public static void ChangeScene(string name)
    {
        SceneManager.LoadScene(name);
    }

    public static InputAction AddBindingsToMap(InputAction action, ReadOnlyArray<InputBinding> bindings)
    {
        foreach (InputBinding binding in bindings)
        {
            if (action.name == "Up" || action.name == "Down" || action.name == "Left" || action.name == "Right")
            {
                action.AddBinding(binding).WithProcessor("axisDeadzone(min=0.9)");
            }
            else
            {
                action.AddBinding(binding);
            }
        }

        return action;
    }

    public static InputActionMap CreateCloneMap(InputActionMap map)
    {
        float time = Time.realtimeSinceStartup;
        InputActionMap newMap = new InputActionMap(map.name + time);
        foreach (InputAction action in map.actions)
        {
            AddBindingsToMap(newMap.AddAction(action.name), action.bindings);
        }

        return newMap;
    }

    public static Texture2D CreatePalette(Color[] colors)
    {
        // Create a new horizontal 1 dimensional texture
        Texture2D palette = new Texture2D(colors.Length, 1);
        palette.filterMode = FilterMode.Point;
        palette.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < colors.Length; i++)
        {
            palette.SetPixel(i, 0, colors[i]);
        }

        palette.Apply();

        return palette;
    }

    public static Sprite CreatePaletteSprite(Color[] colors)
    {
        // Create a new horizontal 1-dimensional texture
        Texture2D palette = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, true);
        for (int i = 0; i < colors.Length; i++)
        {
            palette.SetPixel(i, 0, colors[i]);
        }

        palette.Apply();

        // Create a sprite from the texture
        Sprite paletteSprite = Sprite.Create(palette, new Rect(0, 0, palette.width, palette.height), new Vector2(0.5f, 0.5f), 100f);

        return paletteSprite;
    }
}

using UnityEngine.InputSystem;
using System;
using TMPro;

public static class ButtonPromptCompleter
{
    public static string ReadAndReplaceBinding(string textToDisplay, string stringToReplace, InputBinding actionNeeded, TMP_SpriteAsset spriteAsset, bool pressedOverride)
    {
        if (string.IsNullOrEmpty(textToDisplay) || string.IsNullOrEmpty(stringToReplace) || spriteAsset == null)
        {
            return textToDisplay ?? string.Empty;
        }

        string stringButtonName = GetInputString(actionNeeded, pressedOverride);
        return textToDisplay.Replace(stringToReplace, $"<sprite=\"{spriteAsset.name}\" name=\"{stringButtonName}\">");
    }

    private static string GetInputString(InputBinding actionNeeded, bool pressedOverride)
    {
        string starterString = actionNeeded.ToString();
        starterString = starterString.Replace($"[{actionNeeded.groups}]", String.Empty);
        starterString = starterString.Replace($"{actionNeeded.action}:", String.Empty);
        starterString = starterString.Replace("Interact:", String.Empty);
        starterString = starterString.Replace("<Keyboard>/", "Keyboard_");
        starterString = starterString.Replace("dpad/", "dpad_");
        starterString = starterString.Replace("Left Stick/", "ls_");
        starterString = starterString.Replace("Right Stick/", "rs_");
        starterString = starterString.Replace("<Gamepad>/", "Gamepad_");
        //this is if we want the key to be pressed
        if (pressedOverride)
        {
            if (starterString.Contains("Keyboard_"))
            {
                starterString = starterString + "Pressed";
            }
            else if (starterString.Contains("Gamepad_"))
            {
                if (starterString.Contains("dpad_"))
                {
                    starterString = "Gamepad_dpad_null";
                }
                else
                {
                    starterString = "Gamepad_buttonNull";
                }
            }
        }

        return starterString;
    }
}

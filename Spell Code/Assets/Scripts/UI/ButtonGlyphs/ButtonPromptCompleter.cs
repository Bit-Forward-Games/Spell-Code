using UnityEngine.InputSystem;
using System;
using TMPro;

public static class ButtonPromptCompleter
{
    public static string ReadAndReplaceBinding(string textToDisplay, InputBinding actionNeeded, TMP_SpriteAsset spriteAsset)
    {
        string stringButtonName = GetInputString(actionNeeded);
        textToDisplay = textToDisplay.Replace("BUTTONPROMPT",$"<sprite=\"{spriteAsset.name}\" name=\"{stringButtonName}\">");
        return textToDisplay;
    }

    private static string GetInputString(InputBinding actionNeeded)
    {
        string starterString = actionNeeded.ToString();
        starterString = starterString.Replace($"[{actionNeeded.groups}]", String.Empty);
        starterString = starterString.Replace($"{actionNeeded.action}:", String.Empty);
        starterString = starterString.Replace("Interact:", String.Empty);
        starterString = starterString.Replace("<Keyboard>/", "Keyboard_");
        starterString = starterString.Replace("D-Pad/", "dpad_");
        starterString = starterString.Replace("Left Stick/", "ls_");
        starterString = starterString.Replace("Right Stick/", "rs_");
        starterString = starterString.Replace("<Gamepad>/", "Gamepad_");

        return starterString;
    }
}

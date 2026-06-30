using UnityEngine.InputSystem;
using System;
using TMPro;

public static class ButtonPromptCompleter
{
    public static String ReadAndReplaceBinding(string textToDisplay, InputBinding actionNeeded, TMP_SpriteAsset spriteAsset)
    {
        string stringButtonName = RenameInput(actionNeeded.ToString());
        textToDisplay = textToDisplay.Replace("BUTTONPROMPT",$"<sprite-\"{spriteAsset.name}\" name=\"{stringButtonName}\">");
        return textToDisplay;
    }

    private static string RenameInput(string stringButtonName)
    {
        stringButtonName = stringButtonName.Replace("Interact:", String.Empty);
        stringButtonName = stringButtonName.Replace("<Keyboard>/", "Keyboard_");
        stringButtonName = stringButtonName.Replace("D-Pad/", "dpad_");
        //stringButtonName = stringButtonName.Replace("Left Stick/", "ls_");
        //stringButtonName = stringButtonName.Replace("Right Stick/", "rs_");
        stringButtonName = stringButtonName.Replace("<Gamepad>/", "Gamepad_");

        return stringButtonName;
    }
}

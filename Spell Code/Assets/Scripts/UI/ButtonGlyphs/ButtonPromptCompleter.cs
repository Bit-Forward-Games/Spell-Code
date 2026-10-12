using UnityEngine.InputSystem;
using System;
using TMPro;

public static class ButtonPromptCompleter
{
    public static string ReadAndReplaceBinding(string textToDisplay, string stringToReplace, InputBinding actionNeeded, TMP_SpriteAsset spriteAsset, bool pressedOverride, bool nullInput)
    {
        if (string.IsNullOrEmpty(textToDisplay) || string.IsNullOrEmpty(stringToReplace) || spriteAsset == null)
        {
            return textToDisplay ?? string.Empty;
        }

        string stringButtonName = GetInputString(actionNeeded, pressedOverride, nullInput);
        return textToDisplay.Replace(stringToReplace, $"<sprite=\"{spriteAsset.name}\" name=\"{stringButtonName}\">");
    }

    /// <summary>
    /// The sprite tag for one of a player's actions on the device they are actually holding, for text
    /// that needs more glyphs than a TextSetter can give it (one token per component). The asset names
    /// are the TMP sprite assets under TextMesh Pro/Resources/Sprite Assets, which TMP resolves by name
    /// from any text. Empty if the player or the action isn't there.
    /// </summary>
    public static string GlyphTagFor(PlayerController player, string actionName)
    {
        InputActionMap actionMap = player != null && player.inputs != null ? player.inputs.PlayerActionMap : null;
        InputAction action = actionMap != null ? actionMap.FindAction(actionName, false) : null;
        if (action == null)
        {
            return string.Empty;
        }

        InputDevice device = player.inputs.ActiveInputDevice;
        bool gamepad = device != null ? device is Gamepad : Gamepad.all.Count > 0;
        string devicePath = gamepad ? "<Gamepad>" : "<Keyboard>";
        foreach (InputBinding binding in action.bindings)
        {
            if (!string.IsNullOrEmpty(binding.effectivePath) && binding.effectivePath.StartsWith(devicePath))
            {
                string spriteAssetName = gamepad ? "ControllerGlyphs" : "KeyboardGlyphs";
                return $"<sprite=\"{spriteAssetName}\" name=\"{GetInputString(binding, false, false)}\">";
            }
        }

        return string.Empty;
    }

    private static string GetInputString(InputBinding actionNeeded, bool pressedOverride, bool nullInput)
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
        }
        if (nullInput)
        {
            if (starterString.Contains("Gamepad_"))
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
            else
            {
                starterString = "Keyboard_null";
            }
        }

        return starterString;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class DefaultInputBindings
{
    // ===== | Functions | =====
    public static string[][] SetControllerBindings(InputDevice device)
    {
        switch (device)
        {
            case Gamepad gamepad:
                return new string[][]
                {
                    new string[] { gamepad.dpad.up.path, gamepad.leftStick.up.path},
                    new string[] { gamepad.dpad.down.path, gamepad.leftStick.down.path },
                    new string[] { gamepad.dpad.left.path, gamepad.leftStick.left.path },
                    new string[] { gamepad.dpad.right.path, gamepad.leftStick.right.path },
                    new string[] { gamepad.buttonWest.path },
                    new string[] { gamepad.buttonNorth.path },
                    new string[] { gamepad.buttonEast.path, gamepad.rightShoulder.path},
                    new string[] { gamepad.buttonSouth.path }
                };
            case Keyboard keyboard:
                return new string[][]
                {
                    new string[] { keyboard.wKey.path },
                    new string[] { keyboard.sKey.path },
                    new string[] { keyboard.aKey.path },
                    new string[] { keyboard.dKey.path },
                    new string[] { keyboard.rKey.path },
                    new string[] { keyboard.tKey.path },
                    new string[] { keyboard.yKey.path },
                    new string[] { keyboard.fKey.path }
                };
            case Joystick joystick:
                ButtonControl[] buttons = GetButtonControlsFromJoystick(joystick);

                if (buttons.Length >= 4)
                {
                    return new string[][]
                    {
                    new string[] { $"{joystick.path}/hat/up", $"{joystick.path}/stick/up" },
                    new string[] { $"{joystick.path}/hat/down", $"{joystick.path}/stick/down" },
                    new string[] { $"{joystick.path}/hat/left", $"{joystick.path}/stick/left" },
                    new string[] { $"{joystick.path}/hat/right", $"{joystick.path}/stick/right" },

                    new string[] { buttons[0].path },
                    new string[] { buttons[1].path },
                    new string[] { buttons[2].path },
                    new string[] { buttons[3].path },
                };
                }
                else
                    return null;
            default:
                return null;
        }
    }

    public static ButtonControl[] GetButtonControlsFromJoystick(Joystick joystick)
    {
        List<ButtonControl> buttons = new List<ButtonControl>();

        foreach (InputControl input in joystick.allControls)
        {
            if (input is ButtonControl button)
            {
                buttons.Add(button);
            }
        }

        if (buttons.Count > 0)
            return buttons.ToArray();
        else
            return null;
    }
}

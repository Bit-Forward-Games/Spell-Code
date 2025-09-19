using System;
using System.ComponentModel;

public enum ButtonState
{
    None,
    Pressed,
    Released,
    Held
}

//converts inputactions into LONG for the network:

public struct InputSnapshot
{
    public InputSnapshot(int direction, ButtonState[] buttonStates)
    {
        this.direction = direction;
        this.buttonStates = buttonStates;
    }

    private int direction;
    private ButtonState[] buttonStates;

    public int Direction { get { return direction; } set { direction = value; } }
    public ButtonState[] ButtonStates { get { return buttonStates; } set { buttonStates = value; } }

    public override string ToString()
    {
        return $"Direction: {direction}\n" +
               $"Code: {buttonStates[0].ToString()}\n" +
               $"Jump: {buttonStates[1].ToString()}\n";
    }

    public bool IsNull()
    {
        return direction == 5 && buttonStates[0] == ButtonState.None &&
               buttonStates[1] == ButtonState.None;
    }

    public void SetNull()
    {
        direction = 5;
        buttonStates[0] = ButtonState.None;
        buttonStates[1] = ButtonState.None;
    }

    public void SetToSnapshot(InputSnapshot snapshot)
    {
        direction = snapshot.Direction;
        buttonStates[0] = snapshot.ButtonStates[0];
        buttonStates[1] = snapshot.ButtonStates[1];
    }
}

public static class InputConverter
{
    public static InputSnapshot[] ConvertToInputArray(byte[][] byteBuffer)
    {
        InputSnapshot[] snapshotArray = new InputSnapshot[byteBuffer.Length];
        int index = 0;

        foreach (byte[] byteArray in byteBuffer)
        {
            int direction = byteArray[0];
            ButtonState[] buttonStates = new ButtonState[4];

            byte[] bits = new byte[2];

            buttonStates[0] = (ButtonState)((byteArray[1] & 0b00000011));
            buttonStates[1] = (ButtonState)((byteArray[1] & 0b00001100) >> 2);

            snapshotArray[index] = new InputSnapshot(direction, buttonStates);

            index++;
        }

        return snapshotArray;
    }

    public static InputSnapshot ConvertToInputSnapshot(byte[] byteArray)
    {
        InputSnapshot snapshot;

        int direction = byteArray[0];
        ButtonState[] buttonStates = new ButtonState[4];

        buttonStates[0] = (ButtonState)((byteArray[1] & 0b00000011));
        buttonStates[1] = (ButtonState)((byteArray[1] & 0b00001100) >> 2);

        snapshot = new InputSnapshot(direction, buttonStates);

        return snapshot;
    }

    public static byte[] ConvertToByteArray(ButtonState[] buttons,
        bool[] directions)
    {
        byte[] byteArray = new byte[2];
        byteArray[0] = ConstructDirectionByte(directions);
        byteArray[1] = ConstructInputBytes(buttons);

        return byteArray;
    }

    public static long ConvertToLong(ButtonState[] buttons,
        bool[] directions)
    {
        byte[] byteArray = new byte[2];
        byteArray[0] = ConstructDirectionByte(directions);
        byteArray[1] = ConstructInputBytes(buttons);


        long inputs = 0;
        inputs |= (long)(byteArray[0] & 0xFF);
        inputs |= (long)(byteArray[1] & 0xFF) << 8;

        return inputs;
    }

    public static short ConvertToShort(ButtonState[] buttons,
        bool[] directions)
    {
        byte[] byteArray = new byte[2];
        byteArray[0] = ConstructDirectionByte(directions);
        byteArray[1] = ConstructInputBytes(buttons);


        short inputs = 0;
        inputs = (short)(byteArray[0] | byteArray[1] << 8);

        return inputs;
    }

    public static short ConvertFromInputSnapshot(InputSnapshot inputSnapshot)
    {

        byte[] byteArray = new byte[2];
        byteArray[0] = (byte)inputSnapshot.Direction;
        byteArray[1] = ConstructInputBytes(inputSnapshot.ButtonStates);


        short inputs = 0;
        inputs = (short)(byteArray[0] | byteArray[1] << 8);

        return inputs;
    }

    public static InputSnapshot ConvertFromShort(short inputs)
    {
        byte[] byteArray = BitConverter.GetBytes(inputs);
        InputSnapshot snapshot;
        int direction = byteArray[0];
        ButtonState[] buttonStates = new ButtonState[4];
        buttonStates[0] = (ButtonState)((byteArray[1] & 0b00000011));
        buttonStates[1] = (ButtonState)((byteArray[1] & 0b00001100) >> 2);
        snapshot = new InputSnapshot(direction, buttonStates);
        return snapshot;
    }

    public static InputSnapshot ConvertFromLong(long inputs)
    {
        byte[] byteArray = BitConverter.GetBytes(inputs);
        InputSnapshot snapshot;

        int direction = byteArray[0];
        ButtonState[] buttonStates = new ButtonState[4];

        buttonStates[0] = (ButtonState)((byteArray[1] & 0b00000011));
        buttonStates[1] = (ButtonState)((byteArray[1] & 0b00001100) >> 2);

        snapshot = new InputSnapshot(direction, buttonStates);

        return snapshot;
    }

    private static byte ConstructDirectionByte(bool[] directions)
    {
        bool up = directions[0];
        bool down = directions[1];
        bool left = directions[2];
        bool right = directions[3];

        if ((up && down && left && right) ||
           (left && right) && !(up && down) ||
           !(left && right) && (up && down))
        {
            return 5;
        }

        if (up && left) return 7;
        if (up && right) return 9;
        if (down && left) return 1;
        if (down && right) return 3;

        if (up) return 8;
        if (down) return 2;
        if (left) return 4;
        if (right) return 6;

        return 5;
    }

    private static byte ConstructInputBytes(ButtonState[] buttons)
    {
        byte inputByte = 0b00000000;

        switch (buttons[0])
        {
            case ButtonState.Pressed:
                inputByte |= 1 << 0;
                inputByte |= 0 << 1;
                break;
            case ButtonState.Held:
                inputByte |= 1 << 0;
                inputByte |= 1 << 1;
                break;
            case ButtonState.Released:
                inputByte |= 0 << 0;
                inputByte |= 1 << 1;
                break;
            case ButtonState.None:
                inputByte |= 0 << 0;
                inputByte |= 0 << 1;
                break;
        }

        switch (buttons[1])
        {
            case ButtonState.Pressed:
                inputByte |= 1 << 2;
                inputByte |= 0 << 3;
                break;
            case ButtonState.Held:
                inputByte |= 1 << 2;
                inputByte |= 1 << 3;
                break;
            case ButtonState.Released:
                inputByte |= 0 << 2;
                inputByte |= 1 << 3;
                break;
            case ButtonState.None:
                inputByte |= 0 << 2;
                inputByte |= 0 << 3;
                break;
        }

        return inputByte;
    }
}

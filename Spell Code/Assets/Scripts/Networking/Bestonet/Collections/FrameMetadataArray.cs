using System;
using UnityEngine;
using static IdolShowdown.Managers.RollbackManager;

public class FrameMetadataArray : CircularArray<FrameMetadata>
{
    private int LatestInsertedFrame = -1;
    public FrameMetadataArray(int size) : base(size)
    {
    }

    public override void Insert(int frame, FrameMetadata value)
    {
        LatestInsertedFrame = frame;
        base.Insert(frame, value);
    }
    public bool ContainsKey(int frame)
    {
        if (Get(frame).frame == frame)
        {
            return true;
        }

        return false;
    }

    public ulong GetInput(int frame)
    {
        if (ContainsKey(frame))
        {
            FrameMetadata data = Get(frame);
            return data.input;
        }
        Debug.Log("Missing input for frame " + frame);
        return 0;
    }

    public int GetLatestFrame()
    {
        return LatestInsertedFrame;
    }
}
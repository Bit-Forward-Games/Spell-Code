using System;
using UnityEngine;
using BestoNet.Collections; // <-- Add this namespace for CircularArray
// using static IdolShowdown.Managers.RollbackManager; // <-- Remove this old static using

// Define FrameMetadataArray either in the same namespace as RollbackManager
// or ensure the necessary using directive is present where it's used.
// Let's assume it's in the same namespace or a globally accessible one for simplicity.

// If FrameMetadata is defined *inside* RollbackManager, you need to refer to it that way.
// We'll use RollbackManager.FrameMetadata

public class FrameMetadataArray : CircularArray<RollbackManager.FrameMetadata> // <-- Use qualified name
{
    private int LatestInsertedFrame = -1;

    // Constructor remains the same
    public FrameMetadataArray(int size) : base(size)
    {
    }

    // Insert method needs the qualified name for the value parameter
    public override void Insert(int frame, RollbackManager.FrameMetadata value) // <-- Use qualified name
    {
        // Check if the value being inserted actually corresponds to the frame index
        // This helps ensure data integrity in the circular buffer
        if (value.frame != frame)
        {
             Debug.LogWarning($"FrameMetadataArray Insert Warning: Inserting metadata for frame {value.frame} at index {frame}. This might indicate an issue.");
             // Depending on design, you might throw an error or just log.
        }

        // Store the latest frame number for which *any* metadata was inserted
        LatestInsertedFrame = Mathf.Max(LatestInsertedFrame, frame); // Use Max to track highest frame seen

        base.Insert(frame, value); // Call the base class Insert
    }

    /// <summary>
    /// Checks if valid metadata (matching the frame number) exists for the given frame index.
    /// </summary>
    public bool ContainsKey(int frame)
    {
        // Retrieve the data stored at the index corresponding to 'frame'
        // The base class Get() handles the modulo arithmetic.
        RollbackManager.FrameMetadata storedData = base.Get(frame); // <-- Call base.Get()

        // Check if the 'frame' field within the stored data matches the requested frame number.
        // Also check if input is non-zero? Or just frame match is enough? Assume frame match is enough.
        // Add a null check or default check if FrameMetadata could be null/default struct.
        // Assuming FrameMetadata is a struct and frame defaults to 0 if not set:
        // Need a way to distinguish between frame 0 and an uninitialized slot if frame 0 is valid.
        // Option 1: Initialize frame to -1.
        // Option 2: Add an IsValid flag to FrameMetadata.
        // Option 3 (Current): Rely on RollbackManager initializing frame 0 state.
        // Let's check if the stored frame matches the requested frame *and* handle frame 0 potentially clashing with default.
        // A simple check might be just storedData.frame == frame. If frame can be negative, more checks needed.
        return storedData.frame == frame; // Simple check: does the stored data's frame match the index?
    }

    /// <summary>
    /// Gets the input for a specific frame, returning 0 if not found.
    /// </summary>
    public ulong GetInput(int frame)
    {
        if (ContainsKey(frame)) // Use the corrected ContainsKey
        {
            // Retrieve using the base class method
            RollbackManager.FrameMetadata data = base.Get(frame); // <-- Call base.Get()
            return data.input;
        }

        // Only log if the requested frame is reasonably expected (e.g., not far in the future)
        // Avoid excessive logging during initial sync or high latency.
        // if (frame <= LatestInsertedFrame) // Example condition
        // {
        //     Debug.LogWarning($"Missing input for frame {frame}");
        // }

        return 0; // Return neutral input if missing
    }

    /// <summary>
    /// Gets the latest frame number for which metadata was inserted.
    /// </summary>
    public int GetLatestFrame()
    {
        return LatestInsertedFrame;
    }
}
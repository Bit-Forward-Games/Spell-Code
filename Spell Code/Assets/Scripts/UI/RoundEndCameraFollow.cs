using BestoNet.Types;
using DG.Tweening.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;


//using UnityEditor.Experimental.GraphView;

//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

public class RoundEndCameraFollow : MonoBehaviour
{
    public int cameraIndex;
    public float damping = 0.1f;
    public Vector3 offset;
    private Vector3 velocity = Vector3.zero;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        // One follower is authored per player SLOT, but a match can have fewer players than slots,
        // so players[cameraIndex] is legitimately null in a 2P or 3P game -- and the round-end UI is
        // exactly when this runs. GameManager itself is also gone during a teardown. Hold position
        // rather than throwing: this fires every LateUpdate, so the console filled with one NRE per
        // frame per empty slot.
        GameManager manager = GameManager.Instance;
        PlayerController[] roster = manager != null ? manager.players : null;
        if (roster == null || cameraIndex < 0 || cameraIndex >= roster.Length)
        {
            return;
        }

        PlayerController player = roster[cameraIndex];
        if (player == null)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        Vector3 cameraPosition = playerPosition + offset;
        transform.position = Vector3.SmoothDamp(transform.position, cameraPosition, ref velocity, damping);
    }
}

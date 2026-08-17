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
        Vector3 playerPosition = GameManager.Instance.players[cameraIndex].transform.position;
        Vector3 cameraPosition = playerPosition + offset;
        transform.position = Vector3.SmoothDamp(transform.position, cameraPosition, ref velocity, damping);
    }
}

using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;
using System.IO;

public class SpellCode_FloppyDisk : MonoBehaviour
{
    public Animator diskAnimator;
    Bounds diskBounds;
    public string diskName;
    public Image shopSprite;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        diskBounds = GetComponent<Collider>().bounds;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class SpellCode_FloppyDisk : MonoBehaviour
{
    public Animator diskAnimator;
    Bounds diskBounds;

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

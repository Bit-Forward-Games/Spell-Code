using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class SpellCode_FloppyDisk : MonoBehaviour
{
    public Animator diskAnimator;
    //Bounds diskBounds;
    public string diskName;
    public Image shopSprite;
    public PlayerController overlappingPlayer = null;

    public float colliderRadius = 16f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.FindAllFloppyDisks();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (CheckPlayerCollision() != null)
        {
            //Do yo thing Billay
            Debug.Log("Player overlapping floppy disk: " + diskName);
        }
    }

    public PlayerController CheckPlayerCollision()
    {
        PlayerController player;
        // check if all active players are within a certain range of the door
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            player = GameManager.Instance.players[i];

            FixedVec2 floppyPos = FixedVec2.FromFloat(transform.position.x, transform.position.y);
            // Compute squared distance (avoid square root):
            Fixed dx = Fixed.Abs(player.position.X - floppyPos.X) / Fixed.FromInt(10);
            Fixed dy = Fixed.Abs(player.position.Y - floppyPos.Y) / Fixed.FromInt(10);
            Fixed distSq = (dx * dx) + (dy * dy);

            // Convert collider radius to Fixed and square it
            Fixed radius = Fixed.FromFloat(colliderRadius / 10);
            Fixed radiusSq = radius * radius;

            // Determine overlap using squared values
            if (distSq < radiusSq)
            {
                return player;
            }
        }

        return null;
    }
}

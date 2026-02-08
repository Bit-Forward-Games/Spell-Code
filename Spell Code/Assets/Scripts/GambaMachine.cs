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

public class GambaMachine : MonoBehaviour
{
    public Animator gambaAnimator;
    //Bounds diskBounds;
    public PlayerController ownerPlayer = null;

    public HurtboxData hurtbox = new HurtboxData();
    public float colliderRadius = 16f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.FindAllFloppyDisks();
        hurtbox = new HurtboxData() { height = 20, width = 20, xOffset = -10, yOffset = 20};
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(CheckHitboxCollision())
        {
            Debug.Log("Hitbox collision detected!");
        }
    }


    public bool CheckHitboxCollision()
    {
        if(ownerPlayer == null || ownerPlayer.basicProjectileInstance == null)
        {
            return false;
        }

        return HitboxManager.Instance.ProcessSingleProjectileCollisison(
            ownerPlayer.basicProjectileInstance.GetComponent<BaseProjectile>(), 
            hurtbox, 
            FixedVec2.FromFloat(transform.position.x, transform.position.y), 
            true);
    }
}

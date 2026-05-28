using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class ClearMachine : MonoBehaviour
{
    public Animator clearAnimator;
    public bool isActive;
    public PlayerController ownerPlayer = null;
    public int ownerPID;
    private GameManager gameManager;

    public HurtboxData hurtbox = new HurtboxData();
    public float colliderRadius = 16f;

    public bool facingRight = true;

    public byte resetTimer = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameManager = GameManager.Instance;

        hurtbox = new HurtboxData() { height = 48, width = 20, xOffset = -10, yOffset = 48 };
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        clearAnimator.SetBool("facingLeft", !facingRight);
        clearAnimator.SetBool("isActive", isActive);

        if (ownerPlayer == null) { ownerPlayer = gameManager.players[ownerPID - 1]; }

        if (isActive && CheckHitboxCollision())
        {
            Debug.Log("CLEARING");
            ownerPlayer.ClearSpellList();
            isActive = false;
        }

        if (!isActive)
        {
            //Debug.Log("GAMBA RESET TIMER GOING");
            resetTimer++;

            if (resetTimer > 60)
            {
                isActive = true;
                resetTimer = 0;
            }
        }
    }

    public bool CheckHitboxCollision()
    {
        if (ownerPlayer == null || ownerPlayer.basicProjectileInstance == null ||
            !ProjectileManager.Instance.activeProjectiles.Contains(ownerPlayer.basicProjectileInstance.GetComponent<BaseProjectile>()))
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

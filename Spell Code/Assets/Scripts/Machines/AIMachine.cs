using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Windows;


using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;

public class AIMachine : MonoBehaviour
{
    public Animator aiAnimator;
    public bool isActive;
    public PlayerController ownerPlayer = null;
    public PlayerController targetNPC = null;
    public List<NpcAI> npcBehaviors = new List<NpcAI>();
    public int npcBehaviorIndex = 0;
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
        npcBehaviorIndex = 0;
        targetNPC.npcAI = npcBehaviors[npcBehaviorIndex];
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        aiAnimator.SetBool("facingLeft", !facingRight);
        aiAnimator.SetBool("isActive", isActive);

        if (ownerPlayer == null) { ownerPlayer = gameManager.players[ownerPID - 1]; }

        if (isActive && CheckHitboxCollision())
        {

            //play the clear machine hit
            SFX_Manager.Instance.PlaySound(Sounds.CLEAR_MACHINE_HIT, 1.0f, 1.0f);

            npcBehaviorIndex = (npcBehaviorIndex+1)%npcBehaviors.Count;
            targetNPC.npcAI = npcBehaviors[npcBehaviorIndex];
            targetNPC.npcAI.owner = targetNPC;
            Vector2 tempSpawnVec = GameManager.Instance.GetNPCSpawnPositions()[0];
            targetNPC.SpawnPlayer(FixedVec2.FromFloat(tempSpawnVec.x, tempSpawnVec.y));
            targetNPC.SpawnToast(npcBehaviors[npcBehaviorIndex].BehaviorName,GameManager.colors["white"]);
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

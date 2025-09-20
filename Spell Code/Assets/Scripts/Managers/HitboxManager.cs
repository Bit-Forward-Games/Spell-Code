using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;


public enum ForceHitstunType
{ 
    None,
    Standing,
    Crouching,
    Air,
}

public class HitboxManager : NonPersistantSingleton<HitboxManager>
{
    // ===== | Variables | =====
    PlayerState playerOneState;
    PlayerState playerTwoState;

    string playerOneCharacter;
    string playerTwoCharacter;

    int playerOneFrame;
    int playerTwoFrame;

    Vector2 playerOneOrigin;
    Vector2 playerTwoOrigin;

    bool playerOneIsRight;
    bool playerTwoIsRight;

    private StageCamera cachedForScreenShakeCamera;

    // ===== | Methods | =====
    private void Start()
    {
        BoxRenderer.RenderBoxes = false;
        cachedForScreenShakeCamera = Camera.main.GetComponent<StageCamera>();
    }

    /*public void ProcessCollisions()
    {
        playerOneState = GameSessionManager.Instance.playerControllers[0].state;
        playerTwoState = GameSessionManager.Instance.playerControllers[1].state;

        playerOneCharacter = GameSessionManager.Instance.playerControllers[0].characterName;
        playerTwoCharacter = GameSessionManager.Instance.playerControllers[1].characterName;

        playerOneFrame = GameSessionManager.Instance.playerControllers[0].logicFrame;
        playerTwoFrame = GameSessionManager.Instance.playerControllers[1].logicFrame;

        playerOneOrigin = GameSessionManager.Instance.playerControllers[0].position;
        playerTwoOrigin = GameSessionManager.Instance.playerControllers[1].position;

        playerOneIsRight = GameSessionManager.Instance.playerControllers[0].facingRight;
        playerTwoIsRight = GameSessionManager.Instance.playerControllers[1].facingRight;

        List<HitboxData> activeHitboxes;
        List<HurtboxData> activeHurtboxes;

        // THE SINGLE LOOP APPROACH
        PlayerController[] players = GameSessionManager.Instance.playerControllers;


        //Projectile vs Player Collision
    //    AProjectile[] allProjectiles = ProjectileManager.Instance.poolParentTransform.GetComponentsInChildren<AProjectile>();

    //    foreach (AProjectile projectile in allProjectiles)
    //    {
    //        var activeGroup = projectile.projectileHitboxes[projectile.activeHitboxGroupIndex];
    //        // Combine all hitbox lists into one sequence
    //        var activeProjHit = activeGroup.hitbox1
    //            .Concat(activeGroup.hitbox2)
    //            .Concat(activeGroup.hitbox3)
    //            .Concat(activeGroup.hitbox4);


    //        PlayerController attackingPlayer = players[projectile.owner];
    //        PlayerController defendingPlayer = players[(projectile.owner + 1) % 2];
    //        (HurtboxGroup, List<int>) hurtInfo = GetHurtboxes(defendingPlayer);
    //        GetActiveBoxes(out activeHurtboxes, hurtInfo, defendingPlayer);

    //        foreach (HitboxData hitbox in activeProjHit)
    //        {
    //            foreach (HurtboxData hurtbox in activeHurtboxes)
    //            {
    //                if (CheckCollision(hitbox, projectile.position, hurtbox, defendingPlayer.position,
    //                        projectile.facingRight, defendingPlayer.facingRight))
    //                {
    //                    //if parry logic for projectiles:
    //                    if (!projectile.hitgap)
    //                    {
    //                        var attklvl = attackingPlayer.GetAttackData(hitbox.attackLvl);
    //                        byte hitstopVal = attklvl.Hitstop;
    //                        defendingPlayer.hitstop = hitstopVal;
    //                        defendingPlayer.hitboxData = hitbox;
    //                        defendingPlayer.isHit = true;
    //                        attackingPlayer.hitboxActive = false;
    //                        projectile.SetState(ProjectileState.OnHit); 
    //                        projectile.hitgap = true;
    //                        projectile.hitFrame = projectile.logicFrame;
    //                        cachedForScreenShakeCamera.ScreenShake(hitstopVal / 60.0f, hitstopVal / 2.0f);
    //                    }
    //                }
    //            }
    //            //Debug.Log($"Projectile {projectile.ProjectileID} has hitbox at offset ({hitbox.xOffset}, {hitbox.yOffset}) with damage {hitbox.damage} who is owned by {players[projectile.owner].name}");
    //        }
    //    }
    }*/


    private bool IsLastHitboxInSequence((HitboxGroup, FrameData) hitInfo, HitboxData hitbox)
    {
        HitboxGroup hitboxGroup = hitInfo.Item1;
        FrameData frameData = hitInfo.Item2;

        //for now its fine to only check the first hitbox list in activeboxes since thats how hitbox group collision is set up e.g sword example
        int hitboxIndex = hitboxGroup.hitbox1.IndexOf(hitbox);
        return hitboxIndex == frameData.startFrames.Count - 1;
    }

    private void GetActiveBoxes(out List<HurtboxData> activeHurtboxes, (HurtboxGroup, List<int>) hurtInfo, PlayerController defendingPlayer)
    {
        activeHurtboxes = new List<HurtboxData>();

        if (hurtInfo.Item2.Count == 1)
        {
            activeHurtboxes.Add(hurtInfo.Item1.hurtbox1[0]);
        }
        else
        {
            int renderIndex = -1;
            foreach (int frame in hurtInfo.Item2)
            {
                if (defendingPlayer.logicFrame >= frame) renderIndex++;
            }

            if (hurtInfo.Item1.hurtbox1.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox1[renderIndex]);

            if (hurtInfo.Item1.hurtbox2.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox2[renderIndex]);

            if (hurtInfo.Item1.hurtbox3.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox3[renderIndex]);

            if (hurtInfo.Item1.hurtbox4.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox4[renderIndex]);
        }
    }

    private void GetActiveBoxes(out List<HitboxData> activeHitboxes, (HitboxGroup, FrameData) hitInfo, PlayerController attackingPlayer)
    {
        activeHitboxes = new List<HitboxData>();

        for (int i = 0; i < hitInfo.Item2.startFrames.Count; i++)
        {
            if (attackingPlayer.logicFrame >= hitInfo.Item2.startFrames[i] && attackingPlayer.logicFrame <= hitInfo.Item2.endFrames[i])
            {

                if (attackingPlayer.logicFrame == hitInfo.Item2.startFrames[i] && !attackingPlayer.hitstopActive)
                {
                    attackingPlayer.hitboxActive = true;
                }

                if (hitInfo.Item1.hitbox1.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox1[i]);

                if (hitInfo.Item1.hitbox2.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox2[i]);

                if (hitInfo.Item1.hitbox3.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox3[i]);

                if (hitInfo.Item1.hitbox4.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox4[i]);
            }
        }
    }

    private void GetActiveBoxes(out List<HitboxData> activeHitboxes, out List<HurtboxData> activeHurtboxes, 
        (HitboxGroup, FrameData) hitInfo, (HurtboxGroup, List<int>) hurtInfo, PlayerController attackingPlayer, PlayerController defendingPlayer)
    {
        activeHitboxes = new List<HitboxData>();
        activeHurtboxes = new List<HurtboxData>();

        for (int i = 0; i < hitInfo.Item2.startFrames.Count; i++)
        {
            if (attackingPlayer.logicFrame >= hitInfo.Item2.startFrames[i] && attackingPlayer.logicFrame <= hitInfo.Item2.endFrames[i])
            {
                
                if(attackingPlayer.logicFrame == hitInfo.Item2.startFrames[i] && !attackingPlayer.hitstopActive)
                {
                    attackingPlayer.hitboxActive = true;
                }

                if (hitInfo.Item1.hitbox1.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox1[i]);

                if (hitInfo.Item1.hitbox2.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox2[i]);

                if (hitInfo.Item1.hitbox3.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox3[i]);

                if (hitInfo.Item1.hitbox4.Count > i)
                    activeHitboxes.Add(hitInfo.Item1.hitbox4[i]);
            }
        }

        if (hurtInfo.Item2.Count == 1)
        {
            activeHurtboxes.Add(hurtInfo.Item1.hurtbox1[0]);
        }
        else
        {
            int renderIndex = -1;
            foreach (int frame in hurtInfo.Item2)
            {
                if (defendingPlayer.logicFrame >= frame) renderIndex++;
            }

            if (hurtInfo.Item1.hurtbox1.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox1[renderIndex]);

            if (hurtInfo.Item1.hurtbox2.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox2[renderIndex]);

            if (hurtInfo.Item1.hurtbox3.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox3[renderIndex]);

            if (hurtInfo.Item1.hurtbox4.Count > renderIndex)
                activeHurtboxes.Add(hurtInfo.Item1.hurtbox4[renderIndex]);
        }
    }

    /// <summary>
    /// Check for Collision between a Hitbox and a Hurtbox
    /// </summary>
    /// <param name="hitbox">The hitbox checking collision</param>
    /// <param name="hitboxOrigin">The center of the player who the hitbox belongs too</param>
    /// <param name="hurtbox">The hurtbox checking collision</param>
    /// <param name="hurtboxOrigin">The center of the player who the hurtbox belongs too</param>
    /// <returns></returns>
    private bool CheckCollision(HitboxData hitbox, Vector2 hitboxOrigin, HurtboxData hurtbox, 
        Vector2 hurtboxOrigin, bool playerOneFacingRight, bool playerTwoFacingRight)
    {

        // If either box has no width or height, return false
        if (hurtbox.width == 0 || hurtbox.height == 0)
        {
            return false;
        }
        // Construct Hitbox Boundaries
        float hitboxLeft = hitboxOrigin.x + GetOffsetX(hitbox, playerOneFacingRight);
        float hitboxRight = hitboxOrigin.x + GetOffsetX(hitbox, playerOneFacingRight) + hitbox.width;
        float hitboxTop = hitboxOrigin.y + hitbox.yOffset;
        float hitboxBottom = hitboxOrigin.y + hitbox.yOffset - hitbox.height;
            
        //Debug.Log($"Hit: Left {hitboxLeft} Right {hitboxRight} Top {hitboxTop} Bottom {hitboxBottom}");

        // Construct Hurtbox Boundaries
        float hurtboxLeft = hurtboxOrigin.x + GetOffsetX(hurtbox, playerTwoFacingRight);
        float hurtboxRight = hurtboxOrigin.x + GetOffsetX(hurtbox, playerTwoFacingRight) + hurtbox.width;
        float hurtboxTop = hurtboxOrigin.y + hurtbox.yOffset;
        float hurtboxBottom = hurtboxOrigin.y + hurtbox.yOffset - hurtbox.height;

        //Debug.Log($"Hurt: Left {hurtboxLeft} Right {hurtboxRight} Top {hurtboxTop} Bottom {hurtboxBottom}");

        // Check for Collision using AABB
        if (hitboxLeft < hurtboxRight && 
            hitboxRight > hurtboxLeft && 
            hitboxTop > hurtboxBottom &&
            hitboxBottom < hurtboxTop)
        {
            return true;
        }

        return false;
    }


//    private void OnGUI()
//    {
//        if (!BoxRenderer.RenderBoxes) return;
//        playerOneState = GameSessionManager.Instance.playerControllers[0].state;
//        playerTwoState = GameSessionManager.Instance.playerControllers[1].state;


//        (HurtboxGroup, List<int>) playerOneHurtInfo = GetHurtboxes(GameSessionManager.Instance.playerControllers[0]);
//        (HurtboxGroup, List<int>) playerTwoHurtInfo = GetHurtboxes(GameSessionManager.Instance.playerControllers[1]);


//        DrawHurtBoxes(playerOneOrigin, playerOneHurtInfo, playerOneFrame, playerOneState, playerOneIsRight);
//        DrawHurtBoxes(playerTwoOrigin, playerTwoHurtInfo, playerTwoFrame, playerTwoState, playerTwoIsRight);      

//        if (IsAttackingState(playerOneState))
//        {
//            (HitboxGroup, FrameData) playerOneHitInfo = GetHitboxes(GameSessionManager.Instance.playerControllers[0]);
//            DrawHitBoxes(playerOneOrigin, playerOneHitInfo, playerOneFrame, playerOneState, playerOneIsRight);
//        }

//        if (IsAttackingState(playerTwoState))
//        {
//            (HitboxGroup, FrameData) playerTwoHitInfo = GetHitboxes(GameSessionManager.Instance.playerControllers[1]);
//            DrawHitBoxes(playerTwoOrigin, playerTwoHitInfo, playerTwoFrame, playerTwoState, playerTwoIsRight);
//        }

//        //draw projectile hitboxes
//        var allProjectiles = ProjectileManager.Instance.poolParentTransform.GetComponentsInChildren<AProjectile>();
//        foreach (var projectile in allProjectiles)
//        {
//            HitboxGroup activeGroup = projectile.projectileHitboxes[projectile.activeHitboxGroupIndex];
//            // Combine all hitbox lists into one sequence
//            var activeProjHit = activeGroup.hitbox1
//                .Concat(activeGroup.hitbox2)
//                .Concat(activeGroup.hitbox3)
//                .Concat(activeGroup.hitbox4);
//            foreach (var hitbox in activeProjHit)
//            {
//                Vector2 projOrigin = projectile.position;
//                projOrigin.x += GetOffsetX(hitbox, projectile.facingRight);
//                projOrigin.y += hitbox.yOffset; //i defintely need patrick to fix this
//                BoxRenderer.DrawBox(projOrigin, hitbox.width, hitbox.height, new Color(0, 1, 0, 0.5f));
//            }
//        }
//        if (GameSessionManager.Instance.pauseOnHit && (IsAttackingState(playerOneState) || IsAttackingState(playerTwoState)))
//        {
//            Debug.Break();
//        }
//        else
//        {
//#if UNITY_EDITOR
//            if (UnityEditor.EditorApplication.isPaused)
//            {
//                UnityEditor.EditorApplication.isPaused = false;
//                UnityEditor.EditorApplication.isPlaying = true;
//                GameSessionManager.Instance.pauseOnHit = false;
//            }
//#endif

//        }
//    }

    private void DrawHitBoxes(Vector2 HitBoxOrigin, (HitboxGroup, FrameData) data,
        int logicFrame, PlayerState currentState, bool facingRight)
    {
        Color hitBoxColor = new(1, 0, 0, 0.5f);
        HitboxGroup boxes = data.Item1;
        FrameData frameData = data.Item2;

        for (int i = 0; i < frameData.startFrames.Count; i++)
        {
            if (logicFrame >= frameData.startFrames[i] && logicFrame <= frameData.endFrames[i])
            {
                if (boxes.hitbox1.Count > i)
                    RenderBox(HitBoxOrigin, boxes.hitbox1[i], "1");

                if (boxes.hitbox2.Count > i)
                    RenderBox(HitBoxOrigin, boxes.hitbox2[i], "2");

                if (boxes.hitbox3.Count > i)
                    RenderBox(HitBoxOrigin, boxes.hitbox3[i], "3");

                if (boxes.hitbox4.Count > i)
                    RenderBox(HitBoxOrigin, boxes.hitbox4[i], "4");
            }
        }

        void RenderBox(Vector2 boxOrigin, HitboxData data, string boxIndex)
        {
            // 1) Draw your box as before...
            boxOrigin.x += GetOffsetX(data, facingRight);
            boxOrigin.y += data.yOffset;
            BoxRenderer.DrawBox(boxOrigin, data.width, data.height, hitBoxColor);

            // 2) Compute world → screen center
            Vector3 worldCenter = new Vector3(
                boxOrigin.x + data.width * 0.5f,
                boxOrigin.y + data.height * 0.5f,
                0f
            );
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldCenter);
            if (screenPos.z <= 0f) return;
            screenPos.y = Screen.height - screenPos.y;

            // 3) Prepare style
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 64
            };

            string text = boxIndex.ToString();
            Vector2 textSize = style.CalcSize(new GUIContent(text));

            // 4) Center-aligned rect at screenPos
            Rect labelRect = new Rect(
                screenPos.x - textSize.x * 0.5f,
                screenPos.y - textSize.y * 0.5f,
                textSize.x,
                textSize.y
            );

            // 5) Draw black outline by stamping the text at offsets
            var prevColor = GUI.color;
            style.normal.textColor = Color.black;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var r = new Rect(labelRect.x + dx, labelRect.y + dy, labelRect.width, labelRect.height);
                    GUI.Label(r, text, style);
                }
            }

            // 6) Draw the white text on top
            style.normal.textColor = Color.white;
            GUI.Label(labelRect, text, style);
            GUI.color = prevColor;
        }
    }



    private void DrawHurtBoxes(Vector2 HurtBoxOrigin, (HurtboxGroup, List<int>) data,
         int logicFrame, PlayerState currentState, bool facingRight)
    {
        Color hurtBoxColor = new(0, 0, 1, 0.5f);
        HurtboxGroup boxes = data.Item1;
        List<int> frames = data.Item2;

        if (frames.Count == 1)
        {
            HurtBoxOrigin.x += GetOffsetX(boxes.hurtbox1[0], facingRight);
            HurtBoxOrigin.y += boxes.hurtbox1[0].yOffset;

            BoxRenderer.DrawBox(HurtBoxOrigin, boxes.hurtbox1[0].width, boxes.hurtbox1[0].height, hurtBoxColor);
        }
        else
        {
            int renderIndex = -1;
            foreach (int frame in frames)
            {
                if (logicFrame >= frame) renderIndex++;
            }

            if (boxes.hurtbox1.Count > renderIndex)
                RenderBox(HurtBoxOrigin, boxes.hurtbox1[renderIndex]);

            if (boxes.hurtbox2.Count > renderIndex)
                RenderBox(HurtBoxOrigin, boxes.hurtbox2[renderIndex]);

            if (boxes.hurtbox3.Count > renderIndex)
                RenderBox(HurtBoxOrigin, boxes.hurtbox3[renderIndex]);

            if (boxes.hurtbox4.Count > renderIndex)
                RenderBox(HurtBoxOrigin, boxes.hurtbox4[renderIndex]);
        }

        void RenderBox(Vector2 boxOrigin, HurtboxData data)
        {
            boxOrigin.x += GetOffsetX(data, facingRight);
            boxOrigin.y += data.yOffset;


            BoxRenderer.DrawBox(boxOrigin, data.width, data.height, hurtBoxColor);
        }
    }

    public bool IsAttackingState(PlayerState state)
    {
        return state switch
        {
            PlayerState.CodeWeave => true,
            PlayerState.CodeRelease => true,
            _ => false
        };
    }

    /// <summary>
    /// Get the X Offset for the Hitbox or HurtBox
    /// </summary>
    /// <param name="hitData"></param>
    /// <param name="isRight"></param>
    /// <returns></returns>
    private int GetOffsetX(HitboxData hitData, bool isRight)
    {
        //return isRight ? hitData.xOffset : -hitData.xOffset;
        return isRight ? hitData.xOffset: -(hitData.xOffset + hitData.width);
    }

    /// <summary>
    /// Get the X Offset for the Hitbox or HurtBox
    /// </summary>
    /// <param name="hitData"></param>
    /// <param name="isRight"></param>
    /// <returns></returns>
    private int GetOffsetX(HurtboxData hurtData, bool isRight)
    {
        return isRight ? hurtData.xOffset : -(hurtData.xOffset + hurtData.width);
    }

    //public (HitboxGroup,FrameData) GetHitboxes(PlayerController attackingPlayer)
    //{
    //    (HitboxGroup, FrameData) hitInfo;
    //    //check hitboxes and hurtboxes for that player at that given state
    //    if (attackingPlayer.state == PlayerState.Special1 ||
    //        attackingPlayer.state == PlayerState.Special2 ||
    //        attackingPlayer.state == PlayerState.Special3)
    //    {
    //        hitInfo = attackingPlayer.specialMoves.GetHitData(attackingPlayer.state, attackingPlayer);
    //    }
    //    else
    //    {
    //        hitInfo = CharacterDataDictionary.GetHitboxInfo(attackingPlayer.characterName, attackingPlayer.state);
    //    }
    //    //update here for projectiles 
    //    return hitInfo;
    //}

    public (HurtboxGroup, List<int>) GetHurtboxes(PlayerController defendingPlayer)
    {
        
        return CharacterDataDictionary.GetHurtboxInfo(defendingPlayer.characterName, defendingPlayer.state);
    }

    

}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Idle,
    Run,
    Crouch,
    Jump,
    Hitstun,
    Tech,
    Slide,
    CodeWeave,
    CodeRelease
}

[DisallowMultipleComponent] //we only want one player controller per player
public class PlayerController : MonoBehaviour
{
    //INPUTS
    public InputPlayerBindings inputs;
    [SerializeField] private InputActionAsset playerInputs;
    private InputAction upAction;
    private InputAction downAction;
    private InputAction leftAction;
    private InputAction rightAction;
    private InputAction codeAction;
    private InputAction jumpAction;
    private readonly bool[] direction = new bool[4];
    private readonly bool[] codeButton = new bool[2];
    private readonly bool[] jumpButton = new bool[2];
    private readonly ButtonState[] buttons = new ButtonState[2];
    public InputSnapshot input;
    //public InputSnapshot bufferInput;
    public string characterName = "Slugmancer";

    [HideInInspector]
    public List<int> cancelOptions = new();

    private ushort lerpDelay = 0;
    [NonSerialized]
    public Vector2 position;

    public bool facingRight = true;
    public bool isGrounded = false;
    int forward;
    int backward;

    //leave public to get 
    public float hSpd = 0; //horizontal speed (effectively Velocity)
    public float vSpd = 0; //vertical speed

    //ANIMATION
    public int logicFrame;
    public int animationFrame;
    public PlayerState state;
    public PlayerState prevState;
    [HideInInspector]
    public SpriteRenderer spriteRenderer;

    //Character Data
    private CharacterData charData;
    public float gravity = 1;
    [HideInInspector]
    public float jumpForce = 10;
    private float runSpeed = 0f;


    //MATCH STATS
    public Texture2D[] matchPalette = new Texture2D[2];
    public ushort currrentPlayerHealth = 0;

    // Push Box Variables
    [HideInInspector]
    public float playerWidth;
    [HideInInspector]
    public float playerHeight;
    //public PlayerController opponent;

    [HideInInspector]
    public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;
    public bool hitboxActive = false;
    public ushort stateSpecificArg = 0; //use only between states

    public byte hitstop = 0;
    public ushort comboCounter = 0; //this is technically for the player being hit, so if the combo counter is increasings thats on the hurt player
    public float damageProration = 1f; //this is a multiplier for the damage of the next hit which slowly decreases as the combo goes on
    public bool hitstopActive = false;
    public bool hitstunOverride = false;



    void Start()
    {

    }


    void Update()
    {

    }

    public void PlayerUpdate(long inputs)
    {
        input = InputConverter.ConvertFromLong(inputs);
        forward = facingRight ? 6 : 4;
        backward = facingRight ? 4 : 6;

        /*if ((CheckCameraWall(true,  checkOnly:true) || CheckCameraWall(false, checkOnly:true))){
            Debug.Log($"{characterName} is in camera wall");
        }*/


        CheckHit(input);



        //If the player is in hitstop, effectively skip the player's logic, but update the buffer input for when you leave hitstop
        if (hitstop > 0)
        {
            hitstop--;
            hitboxActive = false;
            hitstopActive = true;


            //if (bufferInput.IsNull())
            //{
            //    bufferInput = BufferInputs(input);
            //}

            return;
        }
        else
        {
            hitstopActive = false;
            //if (!bufferInput.IsNull())
            //{
            //    input.SetToSnapshot(bufferInput);
            //    bufferInput.SetNull();
            //}
        }

        if (!isGrounded)
        {
            vSpd -= gravity;
            if (IsStunnedState(opponent.state) && opponent.CheckWall(opponent.position.x > 0, true) && Mathf.Sign(hSpd) == (facingRight ? -1 : 1))
            {
                LerpHspd(0, 2);
            }
        }

        PlayerState tempState = state;




        //---------------------------------PLAYER UPDATE STATE MACHINE---------------------------------
        switch (state)
        {
            case PlayerState.Idle:

                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }
                //Check Direction Inputs
                if (input.Direction == forward)
                {
                    SetState(PlayerState.WalkForward);
                    break;
                }
                else if (input.Direction == backward)
                {
                    if (CheckBlockRange())
                    {
                        SetState(PlayerState.Block);
                        break;
                    }

                    SetState(PlayerState.WalkBackward);
                    break;
                }
                else if (input.Direction >= 7)
                {
                    SetState(PlayerState.JumpSquat);
                    break;
                }
                else if (input.Direction <= 3)
                {
                    if (input.Direction == backward - 3 && CheckBlockRange())
                    {
                        SetState(PlayerState.CrouchBlock);
                        break;
                    }

                    SetState(PlayerState.Crouch);

                    break;
                }
                LerpHspd(0, 2);
                break;
            case PlayerState.Run:

                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }

                //Check Direction Inputs
                if (input.Direction == backward)
                {
                    SetState(PlayerState.WalkBackward);
                    break;
                }
                else if (input.Direction >= 7)
                {
                    SetState(PlayerState.JumpSquat);
                    break;
                }
                else if (input.Direction <= 3)
                {
                    SetState(PlayerState.Crouch);
                    break;
                }
                else if (input.Direction == 5)
                {
                    SetState(PlayerState.Idle);
                    break;
                }

                //run logic
                hSpd = runSpeed * (facingRight ? 1 : -1);



                break;
            case PlayerState.Crouch:

                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }

                //Check Direction Inputs
                if (input.Direction >= 4 && input.Direction <= 6)
                {
                    SetState(PlayerState.Idle);
                    break;
                }
                else if (input.Direction >= 7)
                {
                    SetState(PlayerState.Jumpsquat);
                    break;
                }

                LerpHspd(0, 3);
                break;
            case PlayerState.Jump:

                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }

                if (CheckBlockRange() && input.Direction % 3 == backward % 3)
                {
                    SetState(PlayerState.JumpBlock);
                    break;
                }


                //air dash inputs
                if (input.Direction == forward &&
                    this.inputs.InputBuffer.SequenceInBuffer(new short[] { (short)forward, 5, (short)forward }, 10) && airdashCounter > 0)
                {

                    SetState(PlayerState.AirDashStart);
                    break;
                }

                if (input.Direction == backward &&
                    this.inputs.InputBuffer.SequenceInBuffer(new short[] { (short)backward, 5, (short)backward },
                        10) && airdashCounter > 0)
                {

                    SetState(PlayerState.AirBackDashStart);
                    break;
                }

                //Check Direction Inputs
                if (isGrounded)
                {
                    SetState(PlayerState.Landing);
                    break;
                }


                break;
            case PlayerState.CodeWeave:
                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Light))
                {
                    SetState(PlayerState.Idle);
                    break;
                }

                LerpHspd(0, 5);
                break;
            case PlayerState.CodeRelease:
                //Check Attack Inputs
                CheckAttackInputs(input);
                if (state != tempState)
                {
                    break;
                }

                if (logicFrame >=
                    CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Medium))
                {
                    SetState(PlayerState.Idle);
                    break;
                }

                LerpHspd(0, 5);
                break;
            case PlayerState.Hitstun:
                CheckFacingRight();
                AdjustBrightnessForParry();
                if (isGrounded)
                {
                    //ground bounce can happen at any point on screen
                    if (hitboxData is { yKnockback: < -5 })
                    {
                        Debug.Log("Floor bounce!");
                        // Formula: vSpd = baseBounce * log(1 + knockbackMagnitude)
                        float baseBounce = 3f;  // Base multiplier (adjust for overall intensity) (might make a character stat down the line)
                        float knockbackMagnitude = -hitboxData.yKnockback * damageProration;  // Convert to positive
                        vSpd = baseBounce * Mathf.Log(1f + knockbackMagnitude);
                        hitboxData = null;
                        break;
                    }
                    SetState(PlayerState.Tech);
                }

                //check regardless of grounded or not
                var rightSource = GetHitSource(true);   // ▶ right
                var leftSource = GetHitSource(false);  // ◀ left

                bool hitRightEdge = rightSource != WallHitSource.None;
                bool hitLeftEdge = leftSource != WallHitSource.None;
                bool hitBoundary = hitRightEdge || hitLeftEdge;

                if (hitBoundary && hitboxData is { xKnockback: > 8 })
                {
                    var usedSource = hitRightEdge ? rightSource : leftSource;
                    string side = hitRightEdge ? "right" : "left";
                    Debug.Log($"{characterName} hit boundary via {usedSource} on {side} edge");

                    // (b) rebound away from the edge we just struck
                    int reboundDir = hitRightEdge ? -1 : 1;   // ▶ edge → bounce left, ◀ edge → bounce right

                    // (c) build the speeds  //2.25 is most consistnet but we should fix on c
                    float baseBounce = usedSource == WallHitSource.CameraWall ? 2.1f : 1.5f;
                    float knockMag = hitboxData.xKnockback * damageProration;
                    hSpd = baseBounce * Mathf.Log(0.5f + knockMag) * reboundDir;
                    vSpd = baseBounce / 1.5f * Mathf.Log(1f + knockMag);

                    hitboxData = null;
                }
                break;
            case PlayerState.Tech:
                if (isGrounded)
                {
                    
                    SetState(PlayerState.Idle);
                    break;
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Tech))
                {
                    
                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }

                break;
        }



        //check player collisions
        ///PlayerWorldCollisionCheck();
        position.x += hSpd;
        position.y += vSpd;
        //handle player animation
        List<int> frameLengths = AnimationManager.Instance.GetFrameLengthsForCurrentState(this);
        animationFrame = GetCurrentFrameIndex(frameLengths, CharacterDataDictionary.GetAnimFrames(characterName, state).loopAnim);
        logicFrame++;
    }

    public void SetState(PlayerState targetState, bool canceling = false)
    {
        

        prevState = state;
        HandleExitLogic(prevState);

        // If omega override is NOT active, proceed with normal logic
        state = targetState;
        HandleEnterState(targetState, in canceling); //in as I don't intend to modify the reference
        cancelOptions.Clear();
        hitstunOverride = false;
    }



    //move logic for each state here
    private void HandleEnterState(PlayerState curstate, in bool canceling)
    {

        //Debug.Log($"Canceling: {canceling}, Strength: {strength}, Counterhitmod: {chmod}");

        bool wasInHitstun = prevState is PlayerState.Hitstun;
        bool isNowHitstun = curstate is PlayerState.Hitstun;

        if (wasInHitstun && !isNowHitstun)
        {
            comboCounter = 0;
            damageProration = 1f;
        }
        logicFrame = 0;
        animationFrame = 0;
        //float knockbackMultiplier = 0;
        switch (curstate)
        {
            case PlayerState.Idle:
                //hitboxData = null;
                break;
            case PlayerState.Run:
                //ProjectileManager.Instance.SpawnVFX(this, 3, -3);
                break;
            case PlayerState.Crouch:
                break;
            case PlayerState.Jump:
                playerHeight = charData.playerHeight / 2;
                break;
            case PlayerState.Hitstun:
                CheckFacingRight();
                AdjustBrightnessForParry();
                if (isGrounded)
                {
                    //ground bounce can happen at any point on screen
                    if (hitboxData is { yKnockback: < -5 })
                    {
                        Debug.Log("Floor bounce!");
                        // Formula: vSpd = baseBounce * log(1 + knockbackMagnitude)
                        float baseBounce = 3f;  // Base multiplier (adjust for overall intensity) (might make a character stat down the line)
                        float knockbackMagnitude = -hitboxData.yKnockback * damageProration;  // Convert to positive
                        vSpd = baseBounce * Mathf.Log(1f + knockbackMagnitude);
                        hitboxData = null;
                        break;
                    }
                    SetState(PlayerState.Tech);
                }

                //check regardless of grounded or not
                var rightSource = GetHitSource(true);   // ▶ right
                var leftSource = GetHitSource(false);  // ◀ left

                bool hitRightEdge = rightSource != WallHitSource.None;
                bool hitLeftEdge = leftSource != WallHitSource.None;
                bool hitBoundary = hitRightEdge || hitLeftEdge;

                if (hitBoundary && hitboxData is { xKnockback: > 8 })
                {
                    var usedSource = hitRightEdge ? rightSource : leftSource;
                    string side = hitRightEdge ? "right" : "left";
                    Debug.Log($"{characterName} hit boundary via {usedSource} on {side} edge");

                    // (b) rebound away from the edge we just struck
                    int reboundDir = hitRightEdge ? -1 : 1;   // ▶ edge → bounce left, ◀ edge → bounce right

                    // (c) build the speeds  //2.25 is most consistnet but we should fix on c
                    float baseBounce = usedSource == WallHitSource.CameraWall ? 2.1f : 1.5f;
                    float knockMag = hitboxData.xKnockback * damageProration;
                    hSpd = baseBounce * Mathf.Log(0.5f + knockMag) * reboundDir;
                    vSpd = baseBounce / 1.5f * Mathf.Log(1f + knockMag);

                    hitboxData = null;
                }
                break;

            case PlayerState.Tech:
                hSpd = facingRight ? -1 : 1;
                vSpd = 5;
                if (isGrounded)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    isGrounded = false;
                }
                comboCounter = 0;
                damageProration = 1f;
                break;
            case PlayerState.CodeWeave:
                //play codeweave sound
                mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
            case PlayerState.CodeRelease:
                mySFXHandler.PlaySound(SoundType.HEAVY_KICK);
                break;
        }
    }
    //exit logic:
    private void HandleExitLogic(PlayerState prevStateparam)
    {
        switch (prevStateparam)
        {
            case PlayerState.Jump:
                playerHeight = charData.playerHeight;
                break;
        }
        stateSpecificArg = 0;
    }

    private bool CheckGrounded()
    {
        float floorYval = StageData.Instance.floorYval;

        if (position.y + vSpd <= floorYval)
        {
            position.y = floorYval;
            vSpd = 0;
            return true;
        }
        return false;
    }


    //public void CheckFacingRight()
    //{
    //    bool shouldFaceRight = position.x < opponent.position.x;

    //    if (facingRight != shouldFaceRight)
    //    {
    //        facingRight = shouldFaceRight;
    //    }

    //    //check for if player is facing the same direction as the opponent
    //    if (position.x == opponent.position.x)
    //    {
    //        facingRight = !opponent.facingRight;
    //    }
    //}

    public void CheckHit(InputSnapshot input)
    {
        bool isCounterHit = false;
        // Check to see if hitboxData is not null if it's not null, that means the player has been attacked
        if (hitboxData != null && isHit)
        {
            //basically ignore hitstun so some other point in the player's logic can handle it uniquely (e.g. Stag Chi Special 2 parry)
            if (hitstunOverride)
            {
                //play the blocked sound
                mySFXHandler.PlaySound(SoundType.BLOCKED);

                return;
            }

            isHit = false;

            

            mySFXHandler.PlaySound(SoundType.DAMAGED);


            //checking for death
            if (hitboxData.damage > currrentPlayerHealth)
            {
                currrentPlayerHealth = 0;
            }
            else
            {
                

                // Reduce health 
                currrentPlayerHealth = (ushort)Math.Max(0, currrentPlayerHealth - (hitboxData.damage * damageProration) - (isCounterHit ? 10 * hitboxData.counterhitMod : 0));


                // Update damage proration
                damageProration *= hitboxData.damageProration;

                // Increment combo counter
                comboCounter++;
            }


            GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));
            //Debug.Log($"Combo Counter: {comboCounter}, Damage Proration: {damageProration}");

            SetState(PlayerState.Hitstun);
        }
    }

    public bool CheckWall(bool rightWall, bool checkOnly = false)
    {
        float wallXval = rightWall ? StageData.Instance.rightWallXval : StageData.Instance.leftWallXval;
        float offset = rightWall ? playerWidth / 2 : -playerWidth / 2;

        // Check if the player has hit the wall and adjust position and speed
        if ((rightWall && position.x + hSpd + playerWidth / 2 >= wallXval) ||
            (!rightWall && position.x + hSpd - playerWidth / 2 <= wallXval))
        {
            if (checkOnly)
            {
                return true;
            }
            position.x = wallXval - offset;
            hSpd = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// wallbounce validation
    /// </summary>
    //public bool CheckCameraWall(bool rightWall, bool checkOnly = false)
    //{
    //    float cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
    //    float cameraMidX = (position.x + opponent.position.x) * 0.5f;
    //    float boundaryX = rightWall
    //        ? cameraMidX + cameraHalfWidth // right edge
    //        : cameraMidX - cameraHalfWidth; // left  edge
    //    //─ will we cross (or are we already flush) next frame? 
    //    const float eps = 0.0001f;
    //    bool crossing = rightWall
    //        ? position.x + hSpd + playerWidth * 1.5f >= boundaryX - eps
    //        : position.x + hSpd - playerWidth * 1.5f <= boundaryX + eps;

    //    if (!crossing) return false;
    //    if (checkOnly) return true; // “peek” mode – just report
    //    return true;
    //}



    //public void CheckCameraCollision()
    //{
    //    // Calculate the camera half width and average player position
    //    float cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
    //    float avgPlayerPositionX = (position.x + opponent.position.x) / 2;

    //    // Calculate the distance between players and check for camera boundary conditions
    //    float playerDistance = Math.Abs((position.x + hSpd) - (opponent.position.x + opponent.hSpd)) + (playerWidth / 2 + opponent.playerWidth / 2);

    //    if (playerDistance >= cameraHalfWidth * 2 && ((position.x < avgPlayerPositionX && hSpd < 0) || (position.x >= avgPlayerPositionX && hSpd > 0)))
    //    {
    //        // Stop both players' horizontal speeds
    //        hSpd = 0;
    //        opponent.hSpd = 0;

    //        // Determine direction and adjust positions based on average position
    //        int direction = position.x > opponent.position.x ? 1 : -1;
    //        position.x = avgPlayerPositionX + (cameraHalfWidth - playerWidth / 2) * direction;
    //        opponent.position.x = avgPlayerPositionX + (cameraHalfWidth - opponent.playerWidth / 2) * -direction;
    //    }
    //}

    /// <summary>
    /// Returns true if this player’s X-position is closer to the center of the stage
    /// (midpoint between leftWallXval and rightWallXval) than to the nearest wall.
    /// </summary>
    public bool IsCloserToStageCenter()
    {
        // 1) compute the absolute center of the stage
        float leftWall = StageData.Instance.leftWallXval;
        float rightWall = StageData.Instance.rightWallXval;
        float stageCenter = (leftWall + rightWall) * 0.5f;

        // 2) distance from player to center
        float distToCenter = Mathf.Abs(position.x - stageCenter);

        // 3) distance to the nearest wall
        float distToLeft = Mathf.Abs(position.x - leftWall);
        float distToRight = Mathf.Abs(position.x - rightWall);
        float distToWall = Mathf.Min(distToLeft, distToRight);

        // 4) are we closer to center than to the wall?
        return distToCenter < distToWall;
    }


    public void LerpHspd(int targetHspd, int lerpval)
    {
        if (lerpDelay >= lerpval)
        {
            lerpDelay = 0;

            // Adjust horizontal speed towards target
            if (hSpd < targetHspd)
            {
                hSpd++;
            }
            else if (hSpd > targetHspd)
            {
                hSpd--;
            }

            // If hSpd is between -1 and 1, set it to 0
            if (Math.Abs(hSpd) < 1)
            {
                hSpd = 0;
            }
        }
        else
        {
            lerpDelay++;
        }
    }
    private void UpdateInputs()
    {
        direction[0] = upAction.inProgress;
        direction[1] = downAction.inProgress;
        direction[2] = leftAction.inProgress;
        direction[3] = rightAction.inProgress;

        codeButton[0] = codeButton[1];
        jumpButton[0] = jumpButton[1];

        codeButton[1] = codeAction.inProgress;
        jumpButton[1] = jumpAction.inProgress;

        buttons[0] = GetCurrentState(codeButton[0], codeButton[1]);
        buttons[1] = GetCurrentState(jumpButton[0], jumpButton[1]);
    }

    public InputSnapshot BufferInputs(InputSnapshot targetInput)
    {
        InputSnapshot resultingInput = InputConverter.ConvertFromLong(5);
        for (int i = 0; i < 4; i++)
        {
            if (targetInput.ButtonStates[i] == ButtonState.Pressed)
            {
                resultingInput.ButtonStates[i] = ButtonState.Pressed;
            }
        }
        if (!resultingInput.IsNull())
        {
            resultingInput.Direction = targetInput.Direction;
        }
        return resultingInput;
    }
    private ButtonState GetCurrentState(bool previous, bool current)
    {
        return current
            ? (previous ? ButtonState.Held : ButtonState.Pressed)
            : (previous ? ButtonState.Released : ButtonState.None);
    }

    //private bool IsSpecialStateActive() =>
    //    state == PlayerState.Special1 ||
    //    state == PlayerState.Special2 ||
    //    state == PlayerState.Special3;

    //private int GetPlayerIndex() =>
    //    Array.IndexOf(GameSessionManager.Instance.playerControllers, this);

}

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
    private InputAction JumpAction;
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
    //public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;
    public bool hitboxActive = false;
    //public ushort stateSpecificArg = 0; //use only between states

    public byte hitstop = 0;
    public ushort comboCounter = 0; //this is technically for the player being hit, so if the combo counter is increasings thats on the hurt player
    public float damageProration = 1f; //this is a multiplier for the damage of the next hit which slowly decreases as the combo goes on
    public bool hitstopActive = false;



    void Start()
    {
        
    }


    void Update()
    {
        
    }

    /*public void PlayerUpdate(long inputs)
    {
        input = InputConverter.ConvertFromLong(inputs);
        forward = facingRight ? 6 : 4;
        backward = facingRight ? 4 : 6;

        /*if ((CheckCameraWall(true,  checkOnly:true) || CheckCameraWall(false, checkOnly:true))){
            Debug.Log($"{characterName} is in camera wall");
        }*/
    /*

        CheckHit(input);



        //If the player is in hitstop, effectively skip the player's logic, but update the buffer input for when you leave hitstop
        if (hitstop > 0)
        {
            hitstop--;
            hitboxActive = false;
            hitstopActive = true;


            if (bufferInput.IsNull())
            {
                bufferInput = BufferInputs(input);
            }

            return;
        }
        else
        {
            hitstopActive = false;
            if (!bufferInput.IsNull())
            {
                input.SetToSnapshot(bufferInput);
                bufferInput.SetNull();
            }
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

        if (omegaInstall)
            foreach (var install in omegaInstalls.OmegaInstalls)
            {
                install.ObserveOpponent(this);
                if (install.overrideStates.Contains(state))
                    install.PerformOmega(this, tempState, forward, backward);
            }

        if (throwInvulCount > 0)
        {
            throwInvulCount--;
        }

        if (!omegaOverride)
        {

            //---------------------------------PLAYER UPDATE STATE MACHINE---------------------------------
            switch (state)
            {
                case PlayerState.Idle:

                    CheckFacingRight();
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
                    CheckFacingRight();

                    //Check Attack Inputs
                    CheckAttackInputs(input);
                    if (state != tempState)
                    {
                        break;
                    }

                    if (CheckBlockRange() && input.Direction % 3 == backward % 3)
                    {
                        SetState(input.Direction == backward - 3 ? PlayerState.CrouchBlock : PlayerState.Block);
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
                        SetState(PlayerState.JumpSquat);
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
                    CheckFacingRight();
                    if (isGrounded)
                    {
                        if (!CheckBlockRange() || input.Direction % 3 == backward % 3)
                        {
                            SetState(input.Direction <= 3 ? PlayerState.CrouchBlock : PlayerState.Block);
                            break;
                        }
                        SetState(PlayerState.Idle);
                        break;
                    }

                    if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Tech))
                    {
                        if (!CheckBlockRange() || input.Direction % 3 == backward % 3)
                        {
                            SetState(input.Direction <= 3 ? PlayerState.CrouchBlock : PlayerState.Block);
                            break;
                        }
                        SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                        break;
                    }

                    break;
            }

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

    public void SetState(PlayerState targetState, bool canceling = false, AttackStrength strength = AttackStrength.None, int counterhitmod = 0)
    {
        //above any set state:
        if (canceling && !cancelOptions.Contains((int)targetState))
        {
            if (targetState == PlayerState.AirDashStart && playerMeter >= 1250 && cancelOptions.Count > 0)
            {
                playerMeter -= 1250;
                airdashCounter++;
                //if opponent is in hitstun of any kind at hitstop before the dash cancel // do we also do blockstun?
                if (opponent.state == PlayerState.StandHitstun ||
                    opponent.state == PlayerState.CrouchingHitstun ||
                    opponent.state == PlayerState.JumpingHitstun)
                {
                    //add to opponent's hitstop the length of airdash from character data dictionary
                    opponent.hitstop = (byte)(CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.AirDashStart) + hitstop);
                }
                //Debug.Log("Air Dash Cancel does not affect counter");
                GameSessionManager.Instance.UpdateMeterBar(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));
            }
            else
            {
                return;
            }
        }

        prevState = state;
        omegaInstalls.TryGetStates(0, out var states);

        if (omegaInstall && states.Contains(prevState))
        {
            //Debug.Log($"exiting omega state and override the player state: {prevState}");
            omegaInstalls.OmegaInstalls[0].ExitOmega(this);
        }
        HandleExitLogic(prevState);

        ////// Check for Omega override
        if (omegaInstall && states.Contains(targetState))
        {
            state = targetState;
            omegaInstalls.OmegaInstalls[0].SetOmega(this, in canceling, strength: strength);
            cancelOptions.Clear();
            AdjustBrightnessForParry();
            hitstunOverride = false;
            return;
        }

        // If omega override is NOT active, proceed with normal logic
        state = targetState;
        HandleEnterState(targetState, in canceling, strength, counterhitmod); //in as I don't intend to modify the reference
        cancelOptions.Clear();
        AdjustBrightnessForParry();
        hitstunOverride = false;
    }



    //move logic for each state here
    private void HandleEnterState(PlayerState curstate, in bool canceling, AttackStrength strength, int chmod = 0)
    {

        //Debug.Log($"Canceling: {canceling}, Strength: {strength}, Counterhitmod: {chmod}");

        bool wasInHitstun = prevState is PlayerState.StandHitstun
            or PlayerState.CrouchingHitstun
            or PlayerState.JumpingHitstun;
        bool isNowHitstun = curstate is PlayerState.StandHitstun
            or PlayerState.CrouchingHitstun
            or PlayerState.JumpingHitstun
            or PlayerState.KDhitstun;

        if (wasInHitstun && !isNowHitstun)
        {
            comboCounter = 0;
            damageProration = 1f;
        }
        logicFrame = 0;
        animationFrame = 0;
        float knockbackMultiplier = 0;
        switch (curstate)
        {
            case PlayerState.Idle:
                hitboxData = null;
                break;
            case PlayerState.WalkForward:
                break;
            case PlayerState.WalkBackward:
                break;
            case PlayerState.Run:
                ProjectileManager.Instance.SpawnVFX(this, 3, -3);
                break;
            case PlayerState.BackDashStart:
                hSpd = facingRight ? -backbashSpeed : backbashSpeed;
                //play dash sound
                mySFXHandler.PlaySound(SoundType.DASH);
                ProjectileManager.Instance.SpawnVFX(this, 3, -3);
                break;
            case PlayerState.BackDash:
                break;
            case PlayerState.Crouch:
                break;
            case PlayerState.JumpSquat:
                if (input.Direction == forward + 3)
                    hSpd = facingRight ? walkSpeed : -walkSpeed;
                else if (input.Direction == backward + 3)
                    hSpd = facingRight ? -walkSpeed : walkSpeed;
                else
                    hSpd = 0;
                //tell animation manager which jump animation to use
                AnimationManager.Instance.SetJumpAnimation(this);
                break;
            case PlayerState.Jump:
                playerHeight = charData.playerHeight / 2;
                break;
            case PlayerState.Landing:
                //play landing sound
                airdashCounter = charData.airdashCounter;
                mySFXHandler.PlaySound(SoundType.LAND);

                break;
            case PlayerState.AirDashStart:
                hSpd = facingRight ? airdashSpeed : -airdashSpeed;
                if (canceling) hSpd += .25f;
                airdashCounter--;
                //play dash sound
                mySFXHandler.PlaySound(SoundType.DASH);

                break;
            case PlayerState.AirDash:
                break;
            case PlayerState.AirBackDashStart:
                hSpd = facingRight ? -airdashSpeed : airdashSpeed;
                airdashCounter--;
                //play dash sound
                mySFXHandler.PlaySound(SoundType.DASH);

                break;
            case PlayerState.AirBackDash:
                break;
            case PlayerState.Block:
                break;
            case PlayerState.CrouchBlock:
                break;
            case PlayerState.JumpBlock:
                break;
            case PlayerState.BlockStun:
                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    // …and it’s not a projectile, push the opponent away
                    if (!hitboxData.isProjectile)
                    {
                        opponent.hSpd = -hitboxData.xKnockback
                                        * (opponent.facingRight ? 1.15f : -1.15f);
                    }
                    // else: projectile on wall → no hSpd change
                }
                else
                {
                    // in mid‐air / no wall, always apply block knockback
                    hSpd = hitboxData.xKnockback
                           * (opponent.facingRight ? 0.7f : -0.7f);
                }
                stateSpecificArg = GetAttackData(hitboxData.attackLvl).Blockstun;
                break;
            case PlayerState.CrouchBlockStun:
                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    // only non-projectiles shove off walls
                    if (!hitboxData.isProjectile)
                    {
                        opponent.hSpd = -hitboxData.xKnockback
                                        * (opponent.facingRight ? 1f : -1f);
                    }
                }
                else
                {
                    hSpd = hitboxData.xKnockback
                           * (opponent.facingRight ? 0.5f : -0.5f);
                }
                stateSpecificArg = GetAttackData(hitboxData.attackLvl).Blockstun;
                break;
            case PlayerState.JumpBlockStun:
                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    opponent.hSpd = -hitboxData.xKnockback * (opponent.facingRight ? .5f : -.5f);
                }
                else
                {
                    hSpd = hitboxData.xKnockback * (opponent.facingRight ? .5f : -.5f);
                }

                stateSpecificArg = GetAttackData(hitboxData.attackLvl).Blockstun;
                if (isGrounded)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    isGrounded = false;
                }
                break;
            case PlayerState.StandHitstun:
                // Calculate prorated knockback multiplier
                knockbackMultiplier = !opponent.facingRight ? -(1 + (1 - damageProration) / 2) : 1 + (1 - damageProration) / 2;

                // Only modify hSpd if it's NOT a projectile or NOT hitting a wall
                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    // Projectiles hitting a wall: skip modifying hSpd
                    if (!hitboxData.isProjectile)
                    {
                        opponent.hSpd = -hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier * .6f);
                    }
                }
                else
                {
                    hSpd = hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier);
                }

                stateSpecificArg += GetAttackData(hitboxData.attackLvl).Hitstun;
                stateSpecificArg += (ushort)chmod;
                break;


            case PlayerState.CrouchingHitstun:
                knockbackMultiplier = !opponent.facingRight ? -(1 + (1 - damageProration) / 2) : 1 + (1 - damageProration) / 2;

                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    if (!hitboxData.isProjectile)
                    {
                        opponent.hSpd = -hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier * .5f);
                    }
                }
                else
                {
                    hSpd = hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier);
                }

                stateSpecificArg += (ushort)(GetAttackData(hitboxData.attackLvl).Hitstun + 2);
                stateSpecificArg += (ushort)chmod;
                break;

            case PlayerState.JumpingHitstun:
                // Use the same multiplier formula as for Stand and Crouching
                knockbackMultiplier = !opponent.facingRight ? -(1 + (1 - damageProration) / 2) : 1 + (1 - damageProration) / 2;
                //Debug.Log(knockbackMultiplier);

                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                {
                    if (!hitboxData.isProjectile)
                    {
                        opponent.hSpd = -hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier * .7f);
                    }
                }
                else
                {
                    hSpd = hitboxData.xKnockback * (hitboxData.ignoreKnockBackProration ? 1 : knockbackMultiplier);
                }

                // Vertical speed calculation remains the same as before
                vSpd = hitboxData.yKnockback * (hitboxData.ignoreKnockBackProration ? 1 : (1 - (1 - damageProration) / 2));

                if (isGrounded)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    isGrounded = false;
                }

                AnimationManager.Instance.SetAirHitstun(this);
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
            case PlayerState.KDhitstun:
                //calculate prorated knockback multiplier
                // equation for x kb proration multiplier: 1 + ((1 - damageProration) * 2);
                knockbackMultiplier = facingRight ? -(1 + (1 - damageProration) / 2) : 1 + (1 - damageProration) / 2;
                if (CheckWall(position.x > 0, true) && hitboxData.xKnockback > 0)
                    opponent.hSpd = -hitboxData.xKnockback * knockbackMultiplier;
                else
                    hSpd = hitboxData.xKnockback * knockbackMultiplier;
                vSpd = hitboxData.yKnockback * (1 - (1 - damageProration) / 2);
                AnimationManager.Instance.SetKDHitstun(this);
                if (isGrounded)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    isGrounded = false;
                }
                break;
            case PlayerState.Knockdown:
                hSpd = 0;
                vSpd = 0;
                stateSpecificArg = knockdownFrames;
                break;
            //going to need special hitstun animation manager methood most likely
            //TODO: update for rest of states
            case PlayerState.Special1:
                currentSpecialStrength = strength;
                specialMoves.SpecialMoves[0].EnterSpecial(this);
                //play special 1 sound
                mySFXHandler.PlaySound(SoundType.SPECIAL_1);
                break;
            case PlayerState.Special2:
                currentSpecialStrength = strength;
                specialMoves.SpecialMoves[1].EnterSpecial(this);
                //play special 2 sound
                mySFXHandler.PlaySound(SoundType.SPECIAL_2);
                break;
            case PlayerState.Special3:
                currentSpecialStrength = strength;
                specialMoves.SpecialMoves[2].EnterSpecial(this);
                //play special 3 sound
                mySFXHandler.PlaySound(SoundType.SPECIAL_3);
                break;
            case PlayerState.Parry:
                hitboxData = null;
                //play parry sound
                mySFXHandler.PlaySound(SoundType.PARRY);
                break;
            case PlayerState.Light:
                //play light punch sound
                mySFXHandler.PlaySound(SoundType.LIGHT_PUNCH);
                break;
            case PlayerState.Medium:
                //play medium punch sound
                mySFXHandler.PlaySound(SoundType.MEDIUM_PUNCH);
                break;
            case PlayerState.Heavy:
                //play heavy punch sound
                mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
            case PlayerState.CrouchingLight:
                //play kick sound
                mySFXHandler.PlaySound(SoundType.KICK);
                break;
            case PlayerState.CrouchingMedium:
                //play medium punch sound
                mySFXHandler.PlaySound(SoundType.MEDIUM_PUNCH);
                break;
            case PlayerState.CrouchingHeavy:
                //play heavy punch sound
                mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
            case PlayerState.JumpingLight:
                //play light punch sound
                mySFXHandler.PlaySound(SoundType.LIGHT_PUNCH);
                break;
            case PlayerState.JumpingMedium:
                //play medium punch sound
                mySFXHandler.PlaySound(SoundType.MEDIUM_PUNCH);
                break;
            case PlayerState.JumpingHeavy:
                //play heavy punch sound
                mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
        }
    }
    //exit logic:
    private void HandleExitLogic(PlayerState prevStateparam)
    {
        switch (prevStateparam)
        {
            case PlayerState.Block:
            case PlayerState.CrouchBlock:
            case PlayerState.JumpBlock:
                comboCounter = 0;
                break;

            case PlayerState.JumpSquat:
                mySFXHandler.PlaySound(SoundType.JUMP);
                break;

            case PlayerState.Jump:
                playerHeight = charData.playerHeight;
                break;

            case PlayerState.Special1:
            case PlayerState.Special2:
            case PlayerState.Special3:
                currentSpecialStrength = AttackStrength.None;
                break;

            case PlayerState.BlockStun:
            case PlayerState.CrouchBlockStun:
            case PlayerState.JumpBlockStun:
                // Add additional cleanup logic if needed
                throwInvulCount = throwInvul;
                comboCounter = 0;
                break;

            case PlayerState.StandHitstun:
            case PlayerState.CrouchingHitstun:
            case PlayerState.Tech:
            case PlayerState.JumpingHitstun:
            case PlayerState.KDhitstun:
                throwInvulCount = throwInvul;
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


    public void CheckFacingRight()
    {
        bool shouldFaceRight = position.x < opponent.position.x;

        if (facingRight != shouldFaceRight)
        {
            facingRight = shouldFaceRight;
        }

        //check for if player is facing the same direction as the opponent
        if (position.x == opponent.position.x)
        {
            facingRight = !opponent.facingRight;
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
    public bool CheckCameraWall(bool rightWall, bool checkOnly = false)
    {
        float cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
        float cameraMidX = (position.x + opponent.position.x) * 0.5f;
        float boundaryX = rightWall
            ? cameraMidX + cameraHalfWidth // right edge
            : cameraMidX - cameraHalfWidth; // left  edge
        //─ will we cross (or are we already flush) next frame? 
        const float eps = 0.0001f;
        bool crossing = rightWall
            ? position.x + hSpd + playerWidth * 1.5f >= boundaryX - eps
            : position.x + hSpd - playerWidth * 1.5f <= boundaryX + eps;

        if (!crossing) return false;
        if (checkOnly) return true; // “peek” mode – just report
        return true;
    }



    public void CheckCameraCollision()
    {
        // Calculate the camera half width and average player position
        float cameraHalfWidth = Camera.main.orthographicSize * Camera.main.aspect;
        float avgPlayerPositionX = (position.x + opponent.position.x) / 2;

        // Calculate the distance between players and check for camera boundary conditions
        float playerDistance = Math.Abs((position.x + hSpd) - (opponent.position.x + opponent.hSpd)) + (playerWidth / 2 + opponent.playerWidth / 2);

        if (playerDistance >= cameraHalfWidth * 2 && ((position.x < avgPlayerPositionX && hSpd < 0) || (position.x >= avgPlayerPositionX && hSpd > 0)))
        {
            // Stop both players' horizontal speeds
            hSpd = 0;
            opponent.hSpd = 0;

            // Determine direction and adjust positions based on average position
            int direction = position.x > opponent.position.x ? 1 : -1;
            position.x = avgPlayerPositionX + (cameraHalfWidth - playerWidth / 2) * direction;
            opponent.position.x = avgPlayerPositionX + (cameraHalfWidth - opponent.playerWidth / 2) * -direction;
        }
    }

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

        lightButton[0] = lightButton[1];
        mediumButton[0] = mediumButton[1];
        heavyButton[0] = heavyButton[1];
        specialButton[0] = specialButton[1];

        lightButton[1] = lightAction.inProgress;
        mediumButton[1] = mediumAction.inProgress;
        heavyButton[1] = heavyAction.inProgress;
        specialButton[1] = specialAction.inProgress;

        buttons[0] = GetCurrentState(lightButton[0], lightButton[1]);
        buttons[1] = GetCurrentState(mediumButton[0], mediumButton[1]);
        buttons[2] = GetCurrentState(heavyButton[0], heavyButton[1]);
        buttons[3] = GetCurrentState(specialButton[0], specialButton[1]);
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

    private bool IsSpecialStateActive() =>
        state == PlayerState.Special1 ||
        state == PlayerState.Special2 ||
        state == PlayerState.Special3;

    private int GetPlayerIndex() =>
        Array.IndexOf(GameSessionManager.Instance.playerControllers, this);
*/
}

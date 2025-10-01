﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Idle,
    Run,
    Jump,
    Hitstun,
    Tech,
    Slide,
    CodeWeave,
    CodeRelease
}

public struct AttackData
{
    ushort hitstun;
    byte hitstop;

    public AttackData(ushort hitstun, ushort blockstun, byte hitstop, ushort metergain)
    {
        this.hitstun = hitstun;
        this.hitstop = hitstop;
    }

    public ushort Hitstun { readonly get => hitstun; set => hitstun = value; }
    public byte Hitstop { readonly get => hitstop; set => hitstop = value; }
    public override readonly string ToString() => $"({Hitstun}, {Hitstop})";

}

[DisallowMultipleComponent] //we only want one player controller per player
public class PlayerController : MonoBehaviour
{
    //INPUTS
    public InputPlayerBindings inputs;
    public InputActionAsset playerInputs;
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
    public string characterName = "Code-E";

    [HideInInspector]
    public List<int> cancelOptions = new();

    private ushort lerpDelay = 0;
    [NonSerialized]
    public Vector2 position;

    public bool facingRight = true;
    public bool isGrounded = false;

    //leave public to get 
    public float hSpd = 0; //horizontal speed (effectively Velocity)
    public float vSpd = 0; //vertical speed

    //ANIMATION
    public int logicFrame;
    public int animationFrame;
    public PlayerState state;
    public PlayerState prevState;
    public SpriteRenderer spriteRenderer;

    //Character Data
    public CharacterData charData { get; private set; }
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
    public uint stateSpecificArg = 0; //use only within a state, not between them

    public byte hitstop = 0;
    public ushort comboCounter = 0; //this is technically for the player being hit, so if the combo counter is increasings thats on the hurt player
    public float damageProration = 1f; //this is a multiplier for the damage of the next hit which slowly decreases as the combo goes on
    public bool hitstopActive = false;
    public bool hitstunOverride = false;

    public SpellData[] spellList = new SpellData[8];

    //SFX VARIABLES
    //public SFX_Manager mySFXHandler;

    //Player Data (for data saving and balancing, different from the above Character Data)
    public int spellsFired = 0;
    public int basicsFired = 0;
    public int spellsHit = 0;
    public float timer = 0.0f;
    //public bool timerRunning = false;
    public List<string> times = new List<string>();

    void Start()
    {
        upAction = playerInputs.actionMaps[0].FindAction("Up");
        downAction = playerInputs.actionMaps[0].FindAction("Down");
        leftAction = playerInputs.actionMaps[0].FindAction("Left");
        rightAction = playerInputs.actionMaps[0].FindAction("Right");
        codeAction = playerInputs.actionMaps[0].FindAction("Code");
        jumpAction = playerInputs.actionMaps[0].FindAction("Jump");
        logicFrame = 0;


        //bufferInput = InputConverter.ConvertFromLong(5);

        hitboxData = null;

        //specialMoves.SetupSpecialMoves(characterName);
        InitCharacter();

    }

    //get max health helper func:
    public ushort GetMaxHealth()
    {
        return charData.playerHealth;
    }

    public void InitCharacter()
    {
        //Set Player Values 
        charData = CharacterDataDictionary.GetCharacterData(characterName);
        //print(charData.projectileIds);

        currrentPlayerHealth = charData.playerHealth;
        runSpeed = (float)charData.runSpeed / 10;
        jumpForce = charData.jumpForce;
        playerWidth = charData.playerWidth;
        playerHeight = charData.playerHeight;

        //fill the spell list with the character's initial spells
        for (int i = 0; i < charData.startingInventory.Count && i < spellList.Length; i++)
        {
            SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
            spellList[i] = Instantiate(targetSpell);
            spellList[i].owner = this;
        }

        ProjectileManager.Instance.InitializeAllProjectiles();
    }

    public void InitializePalette(Texture2D palette)
    {
        if (spriteRenderer != null)
        {
            MaterialPropertyBlock propertyBlock = new();
            spriteRenderer.GetPropertyBlock(propertyBlock);
            // Check if the property "_PaletteTex" exists in the shader
            if (spriteRenderer.sharedMaterial != null && spriteRenderer.sharedMaterial.HasProperty("_PaletteTex"))
            {
                // Assign the palette texture to the property block
                propertyBlock.SetTexture("_PaletteTex", palette);
                // Apply the updated property block back to the SpriteRenderer
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
            else
            {
                Debug.LogWarning("Material does not have a '_PaletteTex' property.");
            }
        }
        else
        {
            Debug.LogError("SpriteRenderer is not assigned.");
        }
    }




    /// MOVEMENT CODE
    public long GetInputs()
    {
        long input = 0;
        if (inputs.IsActive)
        {
            input = inputs.UpdateInputs();
        }
        return input;
    }

    void Update()
    {

    }

    

    public void PlayerUpdate(long inputs)
    {
        input = InputConverter.ConvertFromLong(inputs);


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
        }

        PlayerState tempState = state;




        //---------------------------------PLAYER UPDATE STATE MACHINE---------------------------------
        switch (state)
        {
            case PlayerState.Idle:

                //Check Direction Inputs
                if (input.Direction == 6)
                {
                    facingRight = true;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.Direction == 4)
                {
                    facingRight = false;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    SetState(PlayerState.CodeWeave);
                    break;
                }
                else if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    vSpd = jumpForce;
                    SetState(PlayerState.Jump);
                    break;
                }
                LerpHspd(0, 4);
                break;
            case PlayerState.Run:

                //Check Direction Inputs

                
                if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    SetState(PlayerState.CodeWeave);
                    break;
                }
                else if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    vSpd = jumpForce;
                    SetState(PlayerState.Jump);
                    break;
                }
                else if (input.Direction == (facingRight ? 4 : 6))
                {
                    facingRight = !facingRight;
                    break;
                }
                else if (input.Direction == (facingRight ? 6 : 4))
                {
                    //run logic
                    hSpd = runSpeed * (facingRight ? 1 : -1);
                }
                else
                {
                    SetState(PlayerState.Idle);
                }


                break;
            case PlayerState.Jump:

                //Check Direction Inputs
                if (isGrounded)
                {
                    SetState(PlayerState.Idle);
                    break;
                }
                if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    SetState(PlayerState.CodeWeave);
                    break;
                }
                if (input.Direction == 6)
                {
                    //run logic
                    facingRight = true;
                    LerpHspd((int)runSpeed, 3);
                }
                else if (input.Direction == 4)
                {
                    facingRight = false;
                    LerpHspd(-(int)runSpeed, 3);
                }
                else
                {
                    LerpHspd(0, 2);
                }


                break;
            case PlayerState.CodeWeave:

                //keep track of how lojng player is in state for
                timer += Time.deltaTime;

                if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    stateSpecificArg = 0;
                    break;
                }

                if (input.Direction is 5 or 1 or 3 or 7 or 9)
                {
                    //make the last bit in stateSpecificArg a 1 to indicate that a  "null" direction was pressed
                    if((stateSpecificArg & (1u << 4)) == 0)
                    {
                        stateSpecificArg |= 1 << 4;
                        Debug.Log("input Primed");
                        Debug.Log($"currentCode: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                    }

                }
                else if ((stateSpecificArg & (1u << 4)) != 0) //if the 5th bit is a 1, and we have a valid direction input, we can record it
                {
                    byte codeCount = (byte)(stateSpecificArg & 0xF); //get the last 4 bits of stateSpecificArg
                    switch (input.Direction){
                        case 2:
                            // Set the 2 highest significant bits minus 2 bits per codeCount to 00
                            // stateSpecificArg: [high bits ...][codeCount (lowest 4 bits)]
                            // Example: For codeCount = 1, clear bits 31-30; for codeCount = 2, clear bits 29-28, etc.
                            stateSpecificArg |= (uint)(0b00 << (8+(codeCount*2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("down input Pressed!");
                            break;
                        case 4:
                            stateSpecificArg |= (uint)(0b10 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("left input Pressed!");
                            break;
                        case 6:
                            stateSpecificArg |= (uint)(0b01 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("right input Pressed!");
                            break;
                        case 8:
                            stateSpecificArg |= (uint)(0b11 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("up input Pressed!");
                            
                            break;
                        default:
                            stateSpecificArg &= ~(1u << 4);
                            break;
                    }
                    // Increment the last 4 bits of stateSpecificArg by 1
                    stateSpecificArg = (stateSpecificArg & ~0xFu) | (((stateSpecificArg & 0xFu) + 1) & 0xFu);
                    Debug.Log($"currentCode: {Convert.ToString(stateSpecificArg, toBase: 2)}");
                }


                if (input.ButtonStates[0] == ButtonState.Released)
                {
                    //keep track of how long player is in state for
                    times.Add(timer.ToString("F2"));
                    timer = 0;

                    //set the 5th bit to 0 to indicate we are no longer primed
                    stateSpecificArg &= ~(1u << 4);
                    Debug.Log($"your inputted code: {Convert.ToString(stateSpecificArg, toBase: 2)}");
                   // Debug.Log($"the test code:      {Convert.ToString(tempTestSpellInput, toBase: 2)}");

                    for (int i = 0; i < spellList.Length; i++)
                    {
                        if(spellList[i] == null || spellList[i].spellType == SpellType.Passive) break;
                        if (spellList[i].spellInput == stateSpecificArg)
                        {
                            Debug.Log($"You Cast {spellList[i].spellName}!");
                            spellList[i].activateFlag = true;
                            SetState(PlayerState.CodeRelease);

                            //spellcode is fired
                            spellsFired++;

                            break;
                        }
                    }
                    //check if we changed state in the spell loop
                    if (state == PlayerState.CodeRelease) break;

                    //create an instance of your basic spell here
                    BaseProjectile newProjectile = (BaseProjectile)ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                    ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new Vector2(15, 15));
                    SetState(PlayerState.CodeRelease);

                    //basic spell is fired
                    basicsFired++;

                    break;
                }


                LerpHspd(0, 5);
                break;
            case PlayerState.CodeRelease:

                if (logicFrame >=
                    CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.CodeRelease))
                {
                    SetState(isGrounded? PlayerState.Idle:PlayerState.Jump);
                    break;
                }

                LerpHspd(0, 5);
                break;
            case PlayerState.Hitstun:
                if (stateSpecificArg >= hitboxData.hitstun)
                {
                    
                    SetState(PlayerState.Tech);
                }

                //bounce off the ground if we hit it
                if (CheckGrounded(true) && vSpd < 0)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    vSpd = -vSpd;
                }

                
                stateSpecificArg++;
                break;
            case PlayerState.Tech:
                if (isGrounded)
                {
                    
                    SetState(PlayerState.Idle);
                    break;
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Tech)*2)
                {
                    
                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }

                break;
        }


        for (int i = 0; i < spellList.Length; i++)
        {
            if (spellList[i] != null)
            {
                spellList[i].SpellUpdate();
            }
        }

        //check player collisions
        PlayerWorldCollisionCheck();
        position.x += hSpd;
        position.y += vSpd;
        //handle player animation
        List<int> frameLengths = AnimationManager.Instance.GetFrameLengthsForCurrentState(this);
        animationFrame = GetCurrentFrameIndex(frameLengths, CharacterDataDictionary.GetAnimFrames(characterName, state).loopAnim);
        logicFrame++;
    }

    /// <summary>
    /// Returns the current frame index based on the current logic frame
    /// </summary>
    /// <param name="frameLengths"></param>
    int GetCurrentFrameIndex(List<int> frameLengths, bool loopAnim)
    {
        int accumulatedLength = 0;
        int totalAnimationLength = frameLengths.Sum();
        int animFrame = loopAnim ? (logicFrame % totalAnimationLength) : Math.Clamp(logicFrame, 0, totalAnimationLength - 1);

        for (int i = 0; i < frameLengths.Count; i++)
        {
            accumulatedLength += frameLengths[i];
            if (animFrame < accumulatedLength)
            {
                return i; // Return correct frame index
            }
        }
        return 0; // Default to first frame (shouldn't happen)
    }

    public void PlayerWorldCollisionCheck()
    {
        isGrounded = CheckGrounded();
        //CheckCameraCollision();
        CheckWall(facingRight);
        CheckWall(!facingRight);
        //PlayerCollisionCheck();
    }

    public void SetState(PlayerState targetState, bool canceling = false)
    {
        

        prevState = state;
        HandleExitLogic(prevState);
        state = targetState;
        HandleEnterState(targetState, in canceling);
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
                hitboxData = null;
                break;
            case PlayerState.Run:
                //ProjectileManager.Instance.SpawnVFX(this, 3, -3);
                break;
            case PlayerState.Jump:
                playerHeight = charData.playerHeight / 2;
                
                break;
            case PlayerState.Hitstun:
                hSpd = hitboxData.xKnockback * (facingRight? -1:1);
                vSpd = hitboxData.yKnockback;

                if (isGrounded)
                {
                    position.y = StageData.Instance.floorYval + 1;
                    isGrounded = false;
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
                //mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
            case PlayerState.CodeRelease:
                //mySFXHandler.PlaySound(SoundType.HEAVY_KICK);
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

    private bool CheckGrounded(bool checkOnly = false)
    {
        float floorYval = StageData.Instance.floorYval;

        if (position.y + vSpd <= floorYval)
        {
            if (checkOnly)
            {
                return true;
            }
            position.y = floorYval;
            vSpd = 0;
            return true;
        }
        return false;
    }


    public void CheckHit(InputSnapshot input)
    {
        // Check to see if hitboxData is not null if it's not null, that means the player has been attacked
        if (hitboxData != null && isHit)
        {
            //basically ignore hitstun so some other point in the player's logic can handle it uniquely (e.g. Stag Chi Special 2 parry)
            if (hitstunOverride)
            {
                //play the blocked sound
                //mySFXHandler.PlaySound(SoundType.BLOCKED);

                return;
            }

            isHit = false;

            //ignore hit if we are in codeweave and the attack level is less than 2 (basic attack)
            if (state == PlayerState.CodeWeave && hitboxData.attackLvl < 2)
            {
                return;  
            }

            //mySFXHandler.PlaySound(SoundType.DAMAGED);


            //checking for death
            if (hitboxData.damage > currrentPlayerHealth)
            {
                currrentPlayerHealth = 0;
            }
            else
            {
                

                // Reduce health 
                currrentPlayerHealth = (ushort)(currrentPlayerHealth - (int)hitboxData.damage);



                // Increment combo counter
                comboCounter++;
            }


            //GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));

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

    /// NETWORK CODE:
    public void Serialize(BinaryWriter bw)
    {
        bw.Write(position.x);
        bw.Write(position.y);
        bw.Write(hSpd);
        bw.Write(vSpd);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write((byte)state);
        bw.Write((byte)prevState);
        bw.Write(logicFrame); // 🔹 Save current animation frame
        bw.Write(lerpDelay);
        bw.Write(stateSpecificArg);
        bw.Write(hitstop);
        bw.Write(hitboxActive);
        bw.Write(hitstopActive);
        bw.Write(hitstunOverride);
        bw.Write(comboCounter);
        //bw.Write(InputConverter.ConvertFromInputSnapshot(bufferInput));
    }


    public void Deserialize(BinaryReader br)
    {
        position.x = br.ReadSingle();
        position.y = br.ReadSingle();
        hSpd = br.ReadSingle();
        vSpd = br.ReadSingle();
        facingRight = br.ReadBoolean();
        isGrounded = br.ReadBoolean();
        state = (PlayerState)br.ReadByte();
        prevState = (PlayerState)br.ReadByte();
        logicFrame = br.ReadInt32();
        lerpDelay = br.ReadUInt16();
        stateSpecificArg = br.ReadUInt32();
        hitstop = br.ReadByte();
        hitboxActive = br.ReadBoolean();
        hitstopActive = br.ReadBoolean();
        hitstunOverride = br.ReadBoolean();
        comboCounter = br.ReadUInt16();
        //bufferInput = InputConverter.ConvertFromShort(br.ReadInt16());
    }


    public void ResetHealth()
    {
        currrentPlayerHealth = charData.playerHealth;
    }

    //private bool IsSpecialStateActive() =>
    //    state == PlayerState.Special1 ||
    //    state == PlayerState.Special2 ||
    //    state == PlayerState.Special3;

    //private int GetPlayerIndex() =>
    //    Array.IndexOf(GameSessionManager.Instance.playerControllers, this);
    public void CheckForInputs(bool enable)
    {
        inputs.CheckForInputs(enable);
    }
}



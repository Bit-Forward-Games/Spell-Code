using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;
using BestoNet.Types;

// Alias for convenience (optional, but recommended for readability)
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using Unity.VisualScripting;

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

    public bool isAlive = true;
    public SpriteRenderer playerSpriteRenderer;
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
    public FixedVec2 position;

    public bool facingRight = true;
    public bool isGrounded = false;

    //leave public to get 
    public Fixed hSpd = Fixed.FromInt(0); //horizontal speed (effectively Velocity)
    public Fixed vSpd = Fixed.FromInt(0); //vertical speed

    //ANIMATION
    public int logicFrame;
    public int animationFrame;
    public PlayerState state;
    public PlayerState prevState;
    public SpriteRenderer spriteRenderer;

    //Character Data
    public CharacterData charData { get; private set; }
    public Fixed gravity = Fixed.FromInt(1);
    [HideInInspector]
    public Fixed jumpForce = Fixed.FromInt(10);
    public Fixed runSpeed = Fixed.FromInt(0);
    public Fixed slideSpeed = Fixed.FromInt(0);

    //Spell Resource Variables
    public ushort flowState = 0; //the timer for how long you are in flow state
    public const ushort maxFlowState = 300;
    public ushort stockStability = 0; //percentage chance to crit, e.g. 25 = 25% chance
    public ushort demonAura = 0;
    public const ushort maxDemonAura = 100;
    public ushort reps = 0;
    public ushort momentum = 0;
    public bool slimed = false;



    //MATCH STATS
    public Texture2D[] matchPalette = new Texture2D[2];
    public ushort currentPlayerHealth = 0;

    // Push Box Variables
    [HideInInspector]
    public Fixed playerWidth;
    [HideInInspector]
    public Fixed playerHeight;
    //public PlayerController opponent;

    [HideInInspector]
    public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;
    //public bool hitboxActive = false;
    public uint stateSpecificArg = 0; //use only within a state, not between them

    public byte hitstop = 0;
    public ushort comboCounter = 0; //this is technically for the player being hit, so if the combo counter is increasings thats on the hurt player
    public Fixed damageProration = Fixed.FromInt(1); //this is a multiplier for the damage of the next hit which slowly decreases as the combo goes on
    public bool hitstopActive = false;
    public bool hitstunOverride = false;

    public List<SpellData> spellList = new List<SpellData>();
    //public int spellCount = 0;

    //SFX VARIABLES
    //public SFX_Manager mySFXHandler;

    //TMPro
    public TextMeshPro inputDisplay;
    public bool removeInputDisplay;

    [SerializeField]
    public Color colorSuccess;

    //Player Data (for data saving and balancing, different from the above Character Data)
    public int spellsFired = 0;
    public int basicsFired = 0;
    public int spellsHit = 0;
    public Fixed timer = Fixed.FromInt(0);
    //public bool timerRunning = false;
    public List<Fixed> times = new List<Fixed>();

    private void Awake()
    {
        ///playerSpriteRenderer = GetComponent<SpriteRenderer>();
        DontDestroyOnLoad(this.gameObject);
    }
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

    /// <summary>
    /// this function is called to initialize the character's data on start, notably not to simply reset the values during a game
    /// </summary>
    public void InitCharacter()
    {
        //Set Player Values 
        charData = CharacterDataDictionary.GetCharacterData(characterName);
        //print(charData.projectileIds);

        currrentPlayerHealth = charData.playerHealth;
        runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
        slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
        jumpForce = Fixed.FromInt(charData.jumpForce);
        playerWidth = Fixed.FromInt(charData.playerWidth);
        playerHeight = Fixed.FromInt(charData.playerHeight);

        //fill the spell list with the character's initial spells
        for (int i = 0; i < charData.startingInventory.Count /*&& i < spellList.Count*/; i++)
        {
            //SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
            //spellList.Add = Instantiate(targetSpell);
            //spellList[i].owner = this;
            //spellCount++;

            AddSpellToSpellList(charData.startingInventory[i]);
        }
        SpawnPlayer(GameManager.Instance.currentStage.playerSpawnTransform[Array.IndexOf(GameManager.Instance.players, this)]);

        //ProjectileManager.Instance.InitializeAllProjectiles();
    }

    public void SpawnPlayer(Vector2 spawmPos)
    {
        isAlive = true;
        gameObject.GetComponent<SpriteRenderer>().enabled = true;
        position = spawnPos;
        hSpd = Fixed.FromInt(0); 
        vSpd = Fixed.FromInt(0);
        stateSpecificArg = 0;
        currrentPlayerHealth = charData.playerHealth;
        runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
        slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
        jumpForce = Fixed.FromInt(charData.jumpForce);
        playerWidth = Fixed.FromInt(charData.playerWidth);
        playerHeight = Fixed.FromInt(charData.playerHeight);
        SetState(PlayerState.Idle);

        //initialize resources
        flowState = 0;
        stockStability = 0;
        demonAura = 0;
        reps = 0;
        momentum = 0;
        slimed = false;

        

        ProjectileManager.Instance.InitializeAllProjectiles();

    }

    public void AddSpellToSpellList(string spellToAdd)
    {
        if(spellList.Count >= 6)
        {
            Debug.LogWarning("Spell List Full, cannot add more spells!");
            return;
        }
        SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[spellToAdd];
        spellList.Add(Instantiate(targetSpell));
        spellList[spellList.Count-1].owner = this;
        spellList[spellList.Count - 1].LoadSpell();
        ProjectileManager.Instance.InitializeAllProjectiles();

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
    }


    public void ClearSpellList()
    {
        // Destroy all spell GameObjects (iterate backwards to be safe)
        for (int i = spellList.Count - 1; i >= 0; i--)
        {
            SpellData spell = spellList[i];
            if (spell != null)
            {
                // Destroy the MonoBehaviour's GameObject that holds this SpellData
                Destroy(spell.gameObject);
            }
        }

        // Remove all references from the list
        spellList.Clear();
        ProjectileManager.Instance.InitializeAllProjectiles();

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
    }

    public void RemoveSpellFromSpellList(string spellToRemove)
    {
        for(int i = 0; i < spellList.Count; i++)
        {
            if(spellList[i] != null && spellList[i].spellName == spellToRemove)
            {
                Destroy(spellList[i]);
                spellList.RemoveAt(i);
                ProjectileManager.Instance.InitializeAllProjectiles();

                int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
                GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
                return;
            }
        }
        Debug.LogWarning("Spell not found in spell list, cannot remove!");
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
    public ulong GetInputs()
    {
        ulong input = 0;
        if (inputs.IsActive)
        {
            long longInput = inputs.UpdateInputs();
            input = (ulong)longInput;
        }
        return input;
    }

    void Update()
    {

    }



    public void PlayerUpdate(ulong rawInput)
    {
        if (!isAlive)
        {
            if (spriteRenderer.enabled == true)
            {
                spriteRenderer.enabled = false;
            }
            return;

        }
        if (spriteRenderer.enabled == false)
        {
            spriteRenderer.enabled = false;
        }

        if (currentPlayerHealth <= 0)
        {
            isAlive = false;
            return;
        }

        input = InputConverter.ConvertFromLong((long)rawInput);


        CheckHit(input);



        //If the player is in hitstop, effectively skip the player's logic, but update the buffer input for when you leave hitstop
        if (hitstop > 0)
        {
            hitstop--;
            //hitboxActive = false;
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
            // If you had vSpd>0 check for halved gravity:
            if (vSpd > Fixed.FromInt(0))
            {
                vSpd -= gravity;
            }
            else
            {
                vSpd -= gravity / Fixed.FromInt(2); // Halve gravity if falling
            }

        }

        PlayerState tempState = state;




        //---------------------------------PLAYER UPDATE STATE MACHINE---------------------------------
        switch (state)
        {
            case PlayerState.Idle:

                if (!isGrounded)
                {
                    SetState(PlayerState.Jump);
                    break;
                }
                //check for slide input:
                if ( input.Direction < 4 && input.ButtonStates[1] == ButtonState.Pressed)
                {
                    SetState(PlayerState.Slide);
                    break;
                }
                //Check Direction Inputs
                if (input.Direction%3 == 0) //3 6 or 9
                {
                    facingRight = true;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.Direction%3 == 1)// 1 4 or 7
                {
                    facingRight = false;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.ButtonStates[0] is ButtonState.Pressed or ButtonState.Held)
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
                LerpHspd(Fixed.FromInt(0), 4);
                break;
            case PlayerState.Run:

                if (!isGrounded)
                {
                    SetState(PlayerState.Jump);
                    break;
                }

                //check for slide input:
                if (input.Direction < 4 && input.ButtonStates[1] == ButtonState.Pressed)
                {
                    SetState(PlayerState.Slide);
                    break;
                }

                //Check Direction Inputs


                if (input.ButtonStates[0] is ButtonState.Pressed or ButtonState.Held)
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
                else if (input.Direction%3 == (facingRight ? 1 : 0))
                {
                    facingRight = !facingRight;
                    break;
                }
                else if (input.Direction % 3 == (facingRight ? 0 : 1))
                {
                    //run logic
                    LerpHspd(runSpeed * (facingRight ? Fixed.FromInt(1) : Fixed.FromInt(-1)), 0);
                    //hSpd = runSpeed * (facingRight ? 1 : -1);
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
                if (input.ButtonStates[0] is ButtonState.Pressed or ButtonState.Held)
                {
                    SetState(PlayerState.CodeWeave);
                    break;
                }
                if (input.Direction%3 == 0)
                {
                    //run logic
                    facingRight = true;
                    LerpHspd(runSpeed, 3);
                }
                else if (input.Direction%3 == 1)
                {
                    facingRight = false;
                    LerpHspd(-runSpeed, 3);
                }
                else
                {
                    LerpHspd(Fixed.FromInt(0), 2);
                }


                break;
            case PlayerState.CodeWeave:
                //only reset the display at the start of CodeWeave state
                if (removeInputDisplay)
                {
                    removeInputDisplay = false;
                    ClearInputDisplay();
                }

                //keep track of how lojng player is in state for
                timer += Fixed.FromFloat(Time.fixedDeltaTime);

                if (vSpd <= Fixed.FromInt(0) && !isGrounded)
                {
                    gravity = Fixed.Clamp(gravity + Fixed.FromFloat(0.002f), Fixed.FromInt(0), Fixed.FromInt(1));
                }

                //jump button pressed
                if (input.ButtonStates[1] is ButtonState.Pressed or ButtonState.Held)
                {
                    ClearInputDisplay();
                    stateSpecificArg = 0;
                }

                if (input.Direction is 5 or 1 or 3 or 7 or 9)
                {
                    //make the last bit in stateSpecificArg a 1 to indicate that a  "null" direction was pressed
                    if ((stateSpecificArg & (1u << 4)) == 0)
                    {
                        stateSpecificArg |= 1 << 4;
                        //Debug.Log("input Primed");
                        //Debug.Log($"currentCode: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                    }

                }
                byte currentInput = 0b00;

                switch (input.Direction)
                {
                    case 2:
                        currentInput = 0b00;
                        break;
                    case 4:
                        currentInput = 0b10;
                        break;
                    case 6:
                        currentInput = 0b01;
                        break;
                    case 8:
                        currentInput = 0b11;

                        break;
                    default:
                        break;
                }
                byte codeCount = (byte)(stateSpecificArg & 0xF); //get the last 4 bits of stateSpecificArg
                byte lastInputInQueue = (byte)((stateSpecificArg >> (6 + codeCount * 2)) & 0b11);

                //Debug.Log($"currentInput: {Convert.ToString(currentInput, toBase: 2)}");
                //Debug.Log($"stateSpecificArg: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                //Debug.Log($"codeCount: {Convert.ToString(codeCount, toBase: 2)}");
                //Debug.Log($"LastInputInQueue: {Convert.ToString(lastInputInQueue, toBase: 2)}");
                //if(stateSpecificArg == 0b0000_0000_0000_0000_0000_1100_0000_0010)
                //{
                //                       Debug.Log($"LastInputInQueue: {Convert.ToString(lastInputInQueue, toBase: 2)}");
                //}

                if ((stateSpecificArg & (1u << 4)) != 0|| (currentInput != lastInputInQueue && stateSpecificArg != 0)) //if the 5th bit is a 1, and we have a valid direction input, we can record it
                {
                    switch (input.Direction)
                    {
                        case 2:
                            // Set the 2 highest significant bits minus 2 bits per codeCount to 00
                            // stateSpecificArg: [high bits ...][codeCount (lowest 4 bits)]
                            // Example: For codeCount = 1, clear bits 31-30; for codeCount = 2, clear bits 29-28, etc.
                            stateSpecificArg |= (uint)(0b00 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("down input Pressed!");
                            UpdateInputDisplay(2);
                            break;
                        case 4:
                            stateSpecificArg |= (uint)(0b10 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("left input Pressed!");
                            UpdateInputDisplay(4);
                            break;
                        case 6:
                            stateSpecificArg |= (uint)(0b01 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("right input Pressed!");
                            UpdateInputDisplay(6);
                            break;
                        case 8:
                            stateSpecificArg |= (uint)(0b11 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("up input Pressed!");
                            UpdateInputDisplay(8);
                            break;
                        default:
                            //stateSpecificArg &= ~(1u << 4);
                            break;
                    }
                    // Increment the last 4 bits of stateSpecificArg by 1
                    if((stateSpecificArg & (1u << 4)) == 0)
                    {
                        stateSpecificArg = (stateSpecificArg & ~0xFu) | (((stateSpecificArg & 0xFu) + 1) & 0xFu);
                    }
                    //Debug.Log($"currentCode: {Convert.ToString(stateSpecificArg, toBase: 2)}");
                }


                if (input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                {
                    //set the 5th bit to 0 to indicate we are no longer primed
                    stateSpecificArg &= ~(1u << 4);
                    Debug.Log($"your inputted code: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                    SetState(PlayerState.CodeRelease, stateSpecificArg);

                    break;
                }


                LerpHspd(Fixed.FromInt(0), 10);
                break;
            case PlayerState.CodeRelease:
                //allow the display to be reset upon entering CodeWeave state
                removeInputDisplay = true;

                if(input.Direction == 6)
                {
                    facingRight = true;
                }
                else if(input.Direction == 4)
                {
                    facingRight = false;
                }
                if (logicFrame == charData.animFrames.codeReleaseAnimFrames.frameLengths.Take(2).Sum())
                {
                    for (int i = 0; i < spellList.Count; i++)
                    {
                        if (spellList[i].spellInput == stateSpecificArg && spellList[i].spellType == SpellType.Active)
                        {
                            Debug.Log($"You Cast {spellList[i].spellName}!");
                            spellList[i].activateFlag = true;

                            //keep track of how long player is in state for
                            times.Add(timer);
                            timer = Fixed.FromInt(0);

                            //set stateSpecificArg to 255 as it is a value we can never normally set it to, to indicate that we successfully fired a spell
                            stateSpecificArg = 255;

                            //spellcode is fired
                            spellsFired++;

                            //make input display flash green to indicate correct input sequence
                            inputDisplay.color = Color.green;

                            break;
                        }
                    }
                    //check if we set stateSpecificArg to 255, which is otherwise impossible to achieve, in the spell loop
                    if (stateSpecificArg == 255) break;

                    //create an instance of your basic spell here
                    BaseProjectile newProjectile = (BaseProjectile)ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                    ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new FixedVec2(Fixed.FromInt(15), Fixed.FromInt(15)));

                    //basic spell is fired
                    basicsFired++;

                    //make input display flash red to indicate incorrect sequence
                    inputDisplay.color = Color.red;
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.CodeRelease))
                {
                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }

                if (isGrounded)
                {
                    LerpHspd(Fixed.FromInt(0), 5);
                }
                break;
            case PlayerState.Hitstun:
                if (stateSpecificArg <= 0)
                {

                    SetState(PlayerState.Tech);
                }

                ////bounce off the ground if we hit it
                //if (CheckGrounded(true) && vSpd < 0)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    vSpd = -vSpd;
                //}


                stateSpecificArg--;
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
            case PlayerState.Slide:
                LerpHspd(Fixed.FromInt(0), charData.slideFriction);
                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Slide))
                {

                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }
                break;
        }

        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i].procConditions.Contains(ProcCondition.OnUpdate))
            {
                spellList[i].CheckCondition();
            }
        }

        for (int i = 0; i < spellList.Count; i++)
        {
            spellList[i].SpellUpdate();
        }

        UpdateResources();

        //check player collisions
        PlayerWorldCollisionCheck();

        position += new FixedVec2(hSpd, vSpd);
        //position.x += hSpd;
        //position.y += vSpd;
        
        // Check conditions of all spells with the onupdate condition
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i].procConditions.Contains(ProcCondition.OnUpdate))
            {
                spellList[i].CheckCondition();
            }
        }

        for (int i = 0; i < spellList.Count; i++)
        {
            spellList[i].SpellUpdate();
        }
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
        //isGrounded = CheckGrounded();
        //CheckWall(facingRight);
        //CheckWall(!facingRight);
        CheckStageDataSOCollision();
        //CheckCameraCollision();
        //PlayerCollisionCheck();
    }

    public bool CheckStageDataSOCollision(bool checkOnly = false)
    {
        isGrounded = false;
        bool returnVal = false;
        StageDataSO stageDataSO = GameManager.Instance.currentStage;
        if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
        {
            // if there's no stage or no solids at all, still check platforms below (handled later)
            if (stageDataSO == null) return false;
        }

        // --- SOLIDS (unchanged behavior) ---
        if (stageDataSO.solidCenter != null && stageDataSO.solidExtent != null)
        {
            int solidCount = Mathf.Min(stageDataSO.solidCenter.Length, stageDataSO.solidExtent.Length);
            if (solidCount > 0)
            {
                Fixed halfW = playerWidth / Fixed.FromInt(2);
                Fixed halfH = playerHeight / Fixed.FromInt(2);

                // Player AABB for the *next* frame
                // Calculate potential next position based on current position and velocity
                //FixedVec2 nextPosition = position + new FixedVec2(hSpd, vSpd);
                Fixed pMinX = position.X + hSpd - halfW;
                Fixed pMaxX = position.X + hSpd + halfW;
                Fixed pMinY = position.Y + vSpd;
                Fixed pMaxY = position.Y + vSpd + playerHeight;

                for (int i = 0; i < solidCount; i++)
                {
                    FixedVec2 center = FixedVec2.FromFloat(stageDataSO.solidCenter[i].x, stageDataSO.solidCenter[i].y);
                    FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.solidExtent[i].x, stageDataSO.solidExtent[i].y);

                    // Treat extent as half-extents: solid min/max
                    FixedVec2 sMin = center - extent;
                    FixedVec2 sMax = center + extent;

                    // Quick rejection test
                    if (pMaxX < sMin.X || pMinX > sMax.X || pMaxY < sMin.Y || pMinY > sMax.Y)
                    {
                        continue;
                    }

                    // Overlap detected
                    if (checkOnly)
                    {
                        return true;
                    }

                    // Compute penetration amounts
                    Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
                    Fixed overlapY = Fixed.Min(pMaxY, sMax.Y) - Fixed.Max(pMinY, sMin.Y);

                    if (overlapX <= Fixed.FromInt(0) || overlapY <= Fixed.FromInt(0))
                    {
                        // Numerical edge-case: treat as no collision
                        continue;
                    }

                    // Resolve along the smallest penetration axis
                    if (overlapX < overlapY)
                    {
                        // Resolve horizontally
                        if (position.X < center.X)
                        {
                            // Player is left of solid -> push left
                            //position.x -= overlapX;
                            position = new FixedVec2(sMin.X - halfW, position.Y);
                        }
                        else
                        {
                            // Player is right of solid -> push right
                            //position.x += overlapX;
                            position = new FixedVec2(sMin.X + halfW, position.Y);
                        }
                        hSpd = Fixed.FromInt(0);
                    }
                    else
                    {
                        // Resolve vertically
                        if (position.Y < center.Y)
                        {
                            // Player is below solid -> push down
                            //position.y -= overlapY;
                            position = new FixedVec2(position.X, sMin.Y - playerHeight);
                            // If hitting underside, zero vertical speed
                            vSpd = Fixed.FromInt(0);
                        }
                        else
                        {
                            // Player is above solid -> land on top
                            //position.y += overlapY;
                            position = new FixedVec2(position.X, sMax.Y);
                            vSpd = Fixed.FromInt(0);
                            isGrounded = true;
                        }
                    }

                    returnVal = true;
                }
            }
        }

        // --- PLATFORMS (one-way: only collide from above while falling/standing) ---
        if (stageDataSO.platformCenter != null && stageDataSO.platformExtent != null)
        {
            int platformCount = Mathf.Min(stageDataSO.platformCenter.Length, stageDataSO.platformExtent.Length);
            if (platformCount == 0) return false;

            Fixed halfW = playerWidth / Fixed.FromInt(2);
            Fixed halfH = playerHeight / Fixed.FromInt(2);

            // Player AABB
            Fixed pMinX = position.X + hSpd - halfW;
            Fixed pMaxX = position.X + hSpd + halfW;
            Fixed pMinY = position.Y + vSpd;
            Fixed pMaxY = position.Y + vSpd + playerHeight;

            for (int i = 0; i < platformCount; i++)
            {
                FixedVec2 center = FixedVec2.FromFloat(stageDataSO.platformCenter[i].x, stageDataSO.platformCenter[i].y);
                FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.platformExtent[i].x, stageDataSO.platformExtent[i].y);

                // Treat extent as half-extents: platform min/max
                FixedVec2 sMin = center - extent;
                FixedVec2 sMax = center + extent;

                // Quick horizontal rejection (platforms only matter when horizontally overlapping)
                if (pMaxX < sMin.X || pMinX > sMax.X)
                {
                    continue;
                }

                // Quick vertical rejection: platforms are thin surfaces; only consider collisions near the top surface.
                // We'll only allow collision when the player is at or above the platform top and moving downward (or stationary).
                // This implements a simple one-way platform behaviour.
                Fixed platformTop = sMax.Y;
                Fixed platformBottom = sMin.Y;

                // If player is completely below platform top, ignore.
                if (pMaxY <= sMin.Y)
                    continue;

                // Overlap in X direction
                Fixed overlapX = Fixed.Min(pMaxX, sMax.X) - Fixed.Max(pMinX, sMin.X);
                if (overlapX <= Fixed.FromInt(0))
                    continue;

                // If checkOnly is requested and player's AABB intersects platform horizontally and vertically area, report true.
                if (checkOnly)
                {
                    // Only report true for platforms when player is above or intersecting the top surface area
                    if (pMinY < platformTop && pMaxY > sMin.Y)
                        return true;
                    continue;
                }

                // Only land on the platform when the player's bottom is at or above the platform top (or intersecting it)
                // and the player is moving downward (vSpd <= 0) or already essentially resting on it.
                // This avoids blocking the player from jumping up through the platform.
                if (pMinY <= platformTop && position.y > platformBottom && vSpd <= 0f)
                {
                    // Snap player to platform top
                    position = new FixedVec2(position.X, platformTop);
                    vSpd = Fixed.FromInt(0);
                    isGrounded = true;
                    returnVal = true;
                }

                //// Also handle the case where player is already slightly embedded (numerical drift) and not moving upward:
                //if (pMinY < platformTop && pMaxY > platformTop && Mathf.Approximately(vSpd, 0f))
                //{
                //    position.y = platformTop;
                //    isGrounded = true;
                //    returnVal = true;
                //}
            }
        }

        return returnVal;
    }

    public void SetState(PlayerState targetState, uint inputSpellArg = 0)
    {


        prevState = state;
        HandleExitLogic(prevState);
        state = targetState;
        HandleEnterState(targetState, inputSpellArg);
        cancelOptions.Clear();
        hitstunOverride = false;
    }



    //move logic for each state here
    private void HandleEnterState(PlayerState curstate, uint inputSpellArg)
    {


        bool wasInHitstun = prevState is PlayerState.Hitstun;
        bool isNowHitstun = curstate is PlayerState.Hitstun;

        if (wasInHitstun && !isNowHitstun)
        {
            comboCounter = 0;
            damageProration = Fixed.FromInt(1);
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
                //playerHeight = charData.playerHeight / 2;

                break;
            case PlayerState.Hitstun:
                stateSpecificArg = hitboxData.hitstun;
                Fixed xKnockback = Fixed.FromInt(hitboxData.xKnockback);
                Fixed yKnockback = Fixed.FromInt(hitboxData.yKnockback);
                hSpd = xKnockback * (facingRight ? Fixed.FromInt(-1) : Fixed.FromInt(1));
                vSpd = yKnockback;

                //if (isGrounded)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    isGrounded = false;
                //}

                break;

            case PlayerState.Tech:
                hSpd = facingRight ? Fixed.FromInt(-1) : Fixed.FromInt(1);
                vSpd = Fixed.FromInt(5);
                //if (isGrounded)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    isGrounded = false;
                //}
                comboCounter = 0;
                damageProration = Fixed.FromInt(1);
                break;
            case PlayerState.CodeWeave:
                //play codeweave sound
                if (/*vSpd < 0 && */!isGrounded)
                {
                    vSpd = Fixed.FromInt(0);
                    gravity = Fixed.FromInt(0);
                }

                //update the player's spell display to show the spell inputs
                int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
                GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex, true);

                //mySFXHandler.PlaySound(SoundType.HEAVY_PUNCH);
                break;
            case PlayerState.CodeRelease:
                stateSpecificArg = inputSpellArg;
                //mySFXHandler.PlaySound(SoundType.HEAVY_KICK);
                break;

            case PlayerState.Slide:
                hSpd = facingRight ? slideSpeed : -slideSpeed;
                break;
        }
    }
    //exit logic:
    private void HandleExitLogic(PlayerState prevStateparam)
    {
        switch (prevStateparam)
        {
            case PlayerState.Jump:
                //playerHeight = charData.playerHeight;
                break;
            case PlayerState.CodeWeave:
                //update the player's spell display to show the spell names
                int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
                GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex, false);
                gravity = 1;
                break;
            case PlayerState.CodeRelease:
                ClearInputDisplay();
                break;
        }
        stateSpecificArg = 0;
    }

    private bool CheckGrounded(bool checkOnly = false)
    {
        Fixed floorYval = Fixed.FromInt(StageData.Instance.floorYval);

        if (position.Y + vSpd <= floorYval)
        {
            if (checkOnly)
            {
                return true;
            }
            position = new FixedVec2(position.X, floorYval);
            vSpd = Fixed.FromInt(0);
            return true;
        }
        return false;
    }


    /// <summary>
    /// Updates spell resource values each frame
    /// </summary>
    public void UpdateResources()
    {
        //update flow state
        if (flowState > 0)
        {
            flowState--;
        }
    }

    /// <summary>
    /// this function makes the player take damage outside of hitstun, notably from spell effect damage
    /// </summary>
    /// <param name="damageAmount"></param>
    public void TakeEffectDamage(int damageAmount)
    {
        //checking for death
        if (damageAmount > currentPlayerHealth)
        {
            currentPlayerHealth = 0;
            
        }
        else
        {
            // Reduce health 
            currentPlayerHealth = (ushort)((int)currentPlayerHealth - damageAmount);

        }

        Debug.Log($"{characterName} took {damageAmount} effect damage! Current Health: {currentPlayerHealth}");
    }

    public void CheckHit(InputSnapshot input)
    {
        // Check to see if hitboxData is not null if it's not null, that means the player has been attacked
        if (hitboxData != null && isHit)
        {
            PlayerController attacker = hitboxData.parentProjectile.owner;
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
            if (hitboxData.damage > currentPlayerHealth)
            {
                currentPlayerHealth = 0;
            }
            else
            {


                // Reduce health 
                currentPlayerHealth = (ushort)(currentPlayerHealth - (int)hitboxData.damage);



                // Increment combo counter
                comboCounter++;
            }

            

            //GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));

            SetState(PlayerState.Hitstun);

            //call the active on hit proc of the spell that created the projectile that hit us
            if (hitboxData.parentProjectile.ownerSpell != null)
            {
                hitboxData.parentProjectile.ownerSpell.ActiveOnHitProc(this);
            }

            //call the checkProcEffect call of every spell that has ProcEffect.OnHit in the attacker's spell list
            for (int i = 0; i < attacker.spellList.Count; i++)
            {
                if (attacker.spellList[i].procConditions.Contains(ProcCondition.OnHit))
                {
                    attacker.spellList[i].CheckCondition();
                }
            }

            //now call the checkProcEffect call of every spell that has ProcEffect.OnHurt in this player's spell list
            for (int i = 0; i < spellList.Count; i++)
            {
                if (spellList[i].procConditions.Contains(ProcCondition.OnHurt))
                {
                    spellList[i].CheckCondition();
                }
            }
        }
    }


    /// <summary>
    /// This function checks for wall bound collisions and adjusts the player's position and speed accordingly.
    /// </summary>
    /// <param name="rightWall"></param>
    /// <param name="checkOnly"></param>
    /// <returns></returns>
    public bool CheckWall(bool rightWall, bool checkOnly = false)
    {
        Fixed wallXval = Fixed.FromInt(rightWall ? StageData.Instance.rightWallXval : StageData.Instance.leftWallXval);
        Fixed offset = rightWall ? playerWidth / Fixed.FromInt(2) : -playerWidth / Fixed.FromInt(2);

        // Check if the player has hit the wall and adjust position and speed
        if ((rightWall && position.X + hSpd + playerWidth / Fixed.FromInt(2) >= wallXval) ||
            (!rightWall && position.X + hSpd - playerWidth / Fixed.FromInt(2) <= wallXval))
        {
            if (checkOnly)
            {
                return true;
            }
            position = new FixedVec2(wallXval - offset, position.Y);
            hSpd = Fixed.FromInt(0);
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
        Fixed leftWall = Fixed.FromInt(StageData.Instance.leftWallXval);
        Fixed rightWall = Fixed.FromInt(StageData.Instance.rightWallXval);
        Fixed stageCenter = (leftWall + rightWall) / Fixed.FromInt(2);

        // 2) distance from player to center
        Fixed distToCenter = Fixed.Abs(position.X - stageCenter);

        // 3) distance to the nearest wall
        Fixed distToLeft = Fixed.Abs(position.X - leftWall); 
        Fixed distToRight = Fixed.Abs(position.X - rightWall); 
        Fixed distToWall = Fixed.Min(distToLeft, distToRight);

        // 4) are we closer to center than to the wall?
        return distToCenter < distToWall;
    }



    /// <summary>
    /// this function lerps the horizontal speed towards a target value over time deterministically
    /// </summary>
    /// <param name="targetHspd"></param>
    /// <param name="lerpval"></param>
    public void LerpHspd(Fixed targetHspd, int lerpval)
    {
        if (lerpDelay >= lerpval)
        {
            lerpDelay = 0;

            // Adjust horizontal speed towards target
            if (hSpd < targetHspd)
            {
                hSpd += Fixed.FromInt(1);
            }
            else if (hSpd > targetHspd)
            {
                hSpd -= Fixed.FromInt(1);
            }

            // If hSpd is between -1 and 1, set it to 0
            if (Fixed.Abs(hSpd) < Fixed.FromInt(1))
            {
                hSpd = Fixed.FromInt(0);
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
        bw.Write(position.X.RawValue);
        bw.Write(position.Y.RawValue);
        bw.Write(hSpd.RawValue);
        bw.Write(vSpd.RawValue);
        bw.Write(gravity.RawValue);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write((byte)state);
        bw.Write((byte)prevState);
        bw.Write(logicFrame); // 🔹 Save current animation frame
        bw.Write(lerpDelay);
        bw.Write(stateSpecificArg);
        bw.Write(hitstop);
        //bw.Write(hitboxActive);
        bw.Write(hitstopActive);
        bw.Write(hitstunOverride);
        bw.Write(comboCounter);
        bw.Write(currrentPlayerHealth); 
        bw.Write(isAlive); 
        bw.Write(flowState);
        bw.Write(stockStability);
        bw.Write(demonAura);
        bw.Write(reps);
        bw.Write(momentum);
        bw.Write(slimed);

        // Spell List Serialization
        bw.Write(spellList.Count); // Write how many spells are in the list
        for (int i = 0; i < spellList.Count; i++)
        {
            // Write the spell's unique name/ID to identify which spell it is
            bw.Write(spellList[i].spellName); // Assuming spellName is unique and constant

            // Call the spell's own Serialize method (needs to be implemented in SpellData)
            spellList[i].Serialize(bw); // Assumes SpellData has Serialize(BinaryWriter bw)
        }

        //bw.Write(InputConverter.ConvertFromInputSnapshot(bufferInput));
    }


    public void Deserialize(BinaryReader br)
    {
        position = new FixedVec2(new Fixed(br.ReadInt32()), new Fixed(br.ReadInt32())); // Assuming Fixed32 uses int
        hSpd = new Fixed(br.ReadInt32());
        vSpd = new Fixed(br.ReadInt32());
        gravity = new Fixed(br.ReadInt32());
        damageProration = new Fixed(br.ReadInt32());
        timer = new Fixed(br.ReadInt32());
        facingRight = br.ReadBoolean();
        isGrounded = br.ReadBoolean();
        state = (PlayerState)br.ReadByte();
        prevState = (PlayerState)br.ReadByte();
        logicFrame = br.ReadInt32();
        animationFrame = br.ReadInt32();
        lerpDelay = br.ReadUInt16();
        stateSpecificArg = br.ReadUInt32();
        hitstop = br.ReadByte();
        //hitboxActive = br.ReadBoolean();
        hitstopActive = br.ReadBoolean();
        hitstunOverride = br.ReadBoolean();
        comboCounter = br.ReadUInt16();
        currrentPlayerHealth = br.ReadUInt16();
        flowState = br.ReadUInt16();
        stockStability = br.ReadUInt16();
        demonAura = br.ReadUInt16();
        reps = br.ReadUInt16();
        momentum = br.ReadUInt16();
        slimed = br.ReadBoolean();
        //bufferInput = InputConverter.ConvertFromShort(br.ReadInt16());

        // Spell List Deserialization
        int spellCount = br.ReadInt32();
        // Important: Ensure the spellList is the correct size and has the correct spells
        // This is complex. A simple approach if the list order/contents don't change dynamically mid-match:
        if (spellList.Count != spellCount)
        {
            Debug.LogError($"Spell list size mismatch during Deserialize! Expected {spellCount}, got {spellList.Count}. State corruption likely.");
            // Potentially try to rebuild the list based on saved names? Very risky.
            // For simplicity, assuming the list composition is stable during rollback frames.
        }

        for (int i = 0; i < spellCount; i++)
        {
            string spellName = br.ReadString(); // Read the identifier

            // Find the corresponding spell instance in the current list
            SpellData spellInstance = spellList.FirstOrDefault(s => s.spellName == spellName);

            if (spellInstance != null)
            {
                // Call the spell's Deserialize method
                spellInstance.Deserialize(br); // Assumes SpellData has Deserialize(BinaryReader br)
            }
            else
            {
                Debug.LogError($"Spell '{spellName}' not found in list during Deserialize. Skipping spell state.");
                // Need a robust way to handle this, perhaps by skipping the correct number of bytes
                // based on how SpellData.Deserialize is implemented, or failing entirely.
                // For now, this will likely cause complete desync if a spell is missing.
            }
        }
    }


    public void ResetHealth()
    {
        currentPlayerHealth = charData.playerHealth;
    }


    public void ProcEffectUpdate()
    {
        //go through the player's spell list and update any proc effects
        for (int i = 0; i < spellList.Count; i++)
        {
            spellList[i].CheckCondition();
        }
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

    public void UpdateInputDisplay(int direction)
    {
        if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
        {
            //down
            if (direction == 2)
            {
                inputDisplay.text += "DOWN, ";
            }

            //left
            if (direction == 4)
            {
                inputDisplay.text += "LEFT,  ";
            }

            //right
            if (direction == 6)
            {
                inputDisplay.text += "RIGHT, ";
            }

            //up
            if (direction == 8)
            {
                inputDisplay.text += "UP, ";
            }
        } 
    }

    public void ClearInputDisplay()
    {
        if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
        {
            inputDisplay.text = "";
            inputDisplay.color = Color.white;
        }    
    }

    public static string ConvertCodeToString(uint code)
    {
        string codeString = "";
        byte codeCount = (byte)(code & 0xF); //get the last 4 bits of stateSpecificArg
        for (int i = 0; i < codeCount; i++)
        {
            byte currentInput = (byte)((code >> (8 + (i * 2))) & 0b11);
            switch (currentInput)
            {
                case 0b00:
                    codeString += "D ";
                    break;
                case 0b01:
                    codeString += "R ";
                    break;
                case 0b10:
                    codeString += "L ";
                    break;
                case 0b11:
                    codeString += "U ";
                    break;
            }
        }
        return codeString.Trim();
    }
}




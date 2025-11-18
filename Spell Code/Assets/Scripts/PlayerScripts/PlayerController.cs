using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

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
    public Vector2 position;

    public bool facingRight = true;
    public bool isGrounded = false;
    public bool onPlatform = false;

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
    public float runSpeed = 0f;
    public float slideSpeed = 0f;

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
    public float playerWidth;
    [HideInInspector]
    public float playerHeight;
    //public PlayerController opponent;

    [HideInInspector]
    public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;
    //public bool hitboxActive = false;
    public uint stateSpecificArg = 0; //use only within a state, not between them

    public byte hitstop = 0;
    public ushort comboCounter = 0; //this is technically for the player being hit, so if the combo counter is increasings thats on the hurt player
    public float damageProration = 1f; //this is a multiplier for the damage of the next hit which slowly decreases as the combo goes on
    public bool hitstopActive = false;
    public bool hitstunOverride = false;

    public List<SpellData> spellList = new List<SpellData>();
    //public int spellCount = 0;

    //SFX VARIABLES
    //public SFX_Manager mySFXHandler;

    //TMPro
    public TextMeshPro inputDisplay;
    public bool removeInputDisplay;
    public TextMeshPro playerNum;

    [SerializeField]
    public Color colorSuccess;

    //Player Data (for data saving and balancing, different from the above Character Data)
    public int spellsFired = 0;
    public int basicsFired = 0;
    public int spellsHit = 0;
    public float timer = 0.0f;
    //public bool timerRunning = false;
    public List<float> times = new List<float>();

    public int roundsWon;

    public bool chosenSpell = false;

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

        currentPlayerHealth = charData.playerHealth;
        runSpeed = (float)charData.runSpeed / 10;
        slideSpeed = (float)charData.slideSpeed / 10;
        jumpForce = charData.jumpForce;
        playerWidth = charData.playerWidth;
        playerHeight = charData.playerHeight;

        //fill the spell list with the character's initial spells
        for (int i = 0; i < charData.startingInventory.Count /*&& i < spellList.Count*/; i++)
        {
            //SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
            //spellList.Add = Instantiate(targetSpell);
            //spellList[i].owner = this;
            //spellCount++;

            AddSpellToSpellList(charData.startingInventory[i]);
        }

        //temp palette assignment based on player index
        switch (Array.IndexOf(GameManager.Instance.players, this))
        {
            case 0:
                InitializePalette(matchPalette[0]);
                //playerNum.text = "P1";
                playerNum.color = Color.magenta;
                break;
            case 1:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P2";
                playerNum.color = Color.cyan;
                gameObject.GetComponent<SpriteRenderer>().color = Color.cyan;
                break;
            case 2:
                InitializePalette(matchPalette[0]);
                //playerNum.text = "P3";
                playerNum.color = Color.yellow;
                gameObject.GetComponent<SpriteRenderer>().color = Color.yellow;
                break;
            case 3:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P4";
                playerNum.color = Color.green;
                gameObject.GetComponent<SpriteRenderer>().color = Color.green;
                break;
        }
        Vector2 spawnPos = GameManager.Instance.GetSpawnPositions()[Array.IndexOf(GameManager.Instance.players, this)];
        SpawnPlayer(spawnPos);

        //ProjectileManager.Instance.InitializeAllProjectiles();
    }

    public void SpawnPlayer(Vector2 spawnPos)
    {
        isAlive = true;
        gameObject.GetComponent<SpriteRenderer>().enabled = true;
        position = spawnPos;
        hSpd = 0;
        vSpd = 0;
        stateSpecificArg = 0;
        currentPlayerHealth = charData.playerHealth;
        runSpeed = (float)charData.runSpeed / 10;
        slideSpeed = (float)charData.slideSpeed / 10;
        jumpForce = charData.jumpForce;
        playerWidth = charData.playerWidth;
        playerHeight = charData.playerHeight;
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

    /// <summary>
    /// Reset player back to initial state
    /// Called at end of each game
    /// </summary>
    public void ResetPlayer()
    {
        ClearSpellList();

        //fill the spell list with the character's initial spells
        for (int i = 0; i < charData.startingInventory.Count /*&& i < spellList.Count*/; i++)
        {
            //SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
            //spellList.Add = Instantiate(targetSpell);
            //spellList[i].owner = this;
            //spellCount++;

            AddSpellToSpellList(charData.startingInventory[i]);
        }

        roundsWon = 0;
        

        //data
        spellsFired = 0;
        basicsFired = 0;
        spellsHit = 0;
        timer = 0.0f;
        times = new List<float>();

        //passive resources
        flowState = 0;
        stockStability = 0;
        demonAura = 0;
        reps = 0;
        momentum = 0;
        slimed = false;
        comboCounter = 0;

        currentPlayerHealth = 100;
        isAlive = true;
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


    public void PlayerUpdate(long inputs)
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

        input = InputConverter.ConvertFromLong(inputs);


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
            vSpd -= vSpd>0?gravity:gravity/2;

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
                    if(input.Direction == 2 && onPlatform)
                    {
                        break;
                    }
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
                LerpHspd(0, 4);
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
                    LerpHspd((int)runSpeed * (facingRight ? 1 : -1), 0);
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
                if (vSpd > 0 && input.ButtonStates[1] is ButtonState.Released or ButtonState.None)
                {
                    //reapply gravity more strongly to create a variable jump height
                    vSpd -= gravity;
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
                    LerpHspd((int)runSpeed, 3);
                }
                else if (input.Direction%3 == 1)
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
                //only reset the display at the start of CodeWeave state
                if (removeInputDisplay)
                {
                    removeInputDisplay = false;
                    ClearInputDisplay();
                }

                //keep track of how lojng player is in state for
                timer += Time.deltaTime;

                if (vSpd <= 0 && !isGrounded)
                {
                    gravity = Math.Clamp(gravity + .002f, 0, 1f);
                }

                //jump button pressed
                if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    ClearInputDisplay();
                    stateSpecificArg = 0;
                }

                if (input.Direction is 5 or 7 or 1 or 3 or 9)
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

                if (codeCount < 12 && ((stateSpecificArg & (1u << 4)) != 0|| (currentInput != lastInputInQueue && stateSpecificArg != 0))) //if the 5th bit is a 1, and we have a valid direction input, we can record it
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

                inputDisplay.text = ConvertCodeToString(stateSpecificArg);
                if (input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                {
                    //set the 5th bit to 0 to indicate we are no longer primed
                    stateSpecificArg &= ~(1u << 4);
                    Debug.Log($"your inputted code: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                    SetState(PlayerState.CodeRelease, stateSpecificArg);

                    break;
                }


                LerpHspd(0, 10);
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


                //float if holding the jump button
                //if(input.ButtonStates[1] == ButtonState.Pressed && vSpd <=0)
                //{
                //    vSpd = jumpForce;
                //}

                if (logicFrame == charData.animFrames.codeReleaseAnimFrames.frameLengths.Take(2).Sum())
                {
                    for (int i = 0; i < spellList.Count; i++)
                    {
                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter <= 0)
                            {
                            Debug.Log($"You Cast {spellList[i].spellName}!");
                            spellList[i].activateFlag = true;

                            //keep track of how long player is in state for
                            times.Add(timer);
                            timer = 0;

                            //set stateSpecificArg to 255 as it is a value we can never normally set it to, to indicate that we successfully fired a spell
                            stateSpecificArg = 255;

                            //spellcode is fired
                            spellsFired++;

                            //make input display flash green to indicate correct input sequence
                            inputDisplay.color = Color.green;

                            break;
                        }

                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter > 0)
                        {
                            inputDisplay.color = Color.yellow;
                            Debug.Log("COOLDOWN");
                        }
                        else { inputDisplay.color = Color.red; }
                    }
                    //check if we set stateSpecificArg to 255, which is otherwise impossible to achieve, in the spell loop
                    if (stateSpecificArg == 255) break;

                    //create an instance of your basic spell here
                    BaseProjectile newProjectile = (BaseProjectile)ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                    ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new Vector2(15, 15));


                    //basic spell is fired
                    basicsFired++;

                    //make input display flash red to indicate incorrect sequence
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.CodeRelease))
                {
                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }

                if (isGrounded)
                {
                    LerpHspd(0, 5);
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
                LerpHspd(0, charData.slideFriction);
                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Slide))
                {

                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }
                break;
        }


        //Check conditions of all spells with the onupdate condition
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
        position.x += hSpd;
        position.y += vSpd;
        
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

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.tempSpellDisplays[playerIndex].UpdateCooldownDisplay(playerIndex);

        //if the hurtboxgroup at your current logic frame and state has width and height of 0, then make the sprite renderer brighter to indicate invulnerability frames
        if (IsCurrentHurtboxGroupEmpty())
        {

            Color tempColor;
            switch (Array.IndexOf(GameManager.Instance.players, this))
            {
                case 0:
                    tempColor = Color.white;
                    break;
                case 1:
                    tempColor = Color.cyan;
                    break;
                case 2:
                    tempColor = Color.yellow;
                    break;
                case 3:
                    tempColor = Color.green;
                    break;
                default:
                    tempColor = Color.white;
                    break;
            }
            tempColor.r = Math.Clamp(tempColor.r - 0.5f, 0, 1f);
            tempColor.g = Math.Clamp(tempColor.g - 0.5f, 0, 1f);
            tempColor.b = Math.Clamp(tempColor.b - 0.5f, 0, 1f);
            playerSpriteRenderer.color = tempColor;
        }
        else
        {
            switch(Array.IndexOf(GameManager.Instance.players, this))
            {
                case 0:
                    playerSpriteRenderer.color = Color.white;
                    break;
                case 1:
                    playerSpriteRenderer.color = Color.cyan;
                    break;
                case 2:
                    playerSpriteRenderer.color = Color.yellow;
                    break;
                case 3:
                    playerSpriteRenderer.color = Color.green;
                    break;
            }
        }



        //check if we are in gameplay scene and if not, reset health to max to avoid dying in non-gameplay scenes
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Gameplay")
        {
            currentPlayerHealth = charData.playerHealth;
        }
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

    /// <summary>
    /// Returns true when the hurtbox group for the current character/state/frame
    /// has no active hurtboxes or all active hurtboxes are null or have width==0 and height==0.
    /// </summary>
    public bool IsCurrentHurtboxGroupEmpty()
    {
        var hurtInfo = CharacterDataDictionary.GetHurtboxInfo(characterName, state);
        HurtboxGroup group = hurtInfo.Item1;
        List<int> frames = hurtInfo.Item2;

        if (group == null || frames == null)
            return true;

        var activeHurtboxes = new List<HurtboxData>();

        // Single-frame (static) hurtbox group
        if (frames.Count == 1)
        {
            if (group.hurtbox1 != null && group.hurtbox1.Count > 0) activeHurtboxes.Add(group.hurtbox1[0]);
            if (group.hurtbox2 != null && group.hurtbox2.Count > 0) activeHurtboxes.Add(group.hurtbox2[0]);
            if (group.hurtbox3 != null && group.hurtbox3.Count > 0) activeHurtboxes.Add(group.hurtbox3[0]);
            if (group.hurtbox4 != null && group.hurtbox4.Count > 0) activeHurtboxes.Add(group.hurtbox4[0]);
        }
        else
        {
            // Determine which indexed frame we should use for the current logicFrame
            int renderIndex = -1;
            foreach (int f in frames)
            {
                if (logicFrame >= f) renderIndex++;
            }

            // No frame active yet -> consider empty
            if (renderIndex < 0)
                return true;

            if (group.hurtbox1 != null && group.hurtbox1.Count > renderIndex) activeHurtboxes.Add(group.hurtbox1[renderIndex]);
            if (group.hurtbox2 != null && group.hurtbox2.Count > renderIndex) activeHurtboxes.Add(group.hurtbox2[renderIndex]);
            if (group.hurtbox3 != null && group.hurtbox3.Count > renderIndex) activeHurtboxes.Add(group.hurtbox3[renderIndex]);
            if (group.hurtbox4 != null && group.hurtbox4.Count > renderIndex) activeHurtboxes.Add(group.hurtbox4[renderIndex]);
        }

        // If there are no hurtboxes to check, treat as empty (invulnerable)
        if (activeHurtboxes.Count == 0) return true;

        // Return false if any active hurtbox is non-null and has non-zero size
        foreach (var hb in activeHurtboxes)
        {
            if (hb != null && !(hb.width == 0 && hb.height == 0))
                return false;
        }

        return true;
    }

    public bool CheckStageDataSOCollision(bool checkOnly = false)
    {
        isGrounded = false;
        onPlatform = false;
        bool returnVal = false;
        StageDataSO stageDataSO = GameManager.Instance.currentStageIndex<0?GameManager.Instance.lobbySO: GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
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
                float halfW = playerWidth * 0.5f;
                float halfH = playerHeight * 0.5f;

                // Player AABB
                float pMinX = position.x + hSpd - halfW;
                float pMaxX = position.x + hSpd + halfW;
                float pMinY = position.y + vSpd;
                float pMaxY = position.y + vSpd + playerHeight;

                for (int i = 0; i < solidCount; i++)
                {
                    Vector2 center = stageDataSO.solidCenter[i];
                    Vector2 extent = stageDataSO.solidExtent[i];

                    // Treat extent as half-extents: solid min/max
                    Vector2 sMin = center - extent;
                    Vector2 sMax = center + extent;

                    // Quick rejection test
                    if (pMaxX < sMin.x || pMinX > sMax.x || pMaxY < sMin.y || pMinY > sMax.y)
                    {
                        continue;
                    }

                    // Overlap detected
                    if (checkOnly)
                    {
                        return true;
                    }

                    // Compute penetration amounts
                    float overlapX = Mathf.Min(pMaxX, sMax.x) - Mathf.Max(pMinX, sMin.x);
                    float overlapY = Mathf.Min(pMaxY, sMax.y) - Mathf.Max(pMinY, sMin.y);

                    if (overlapX < 0f || overlapY < 0f)
                    {
                        // Numerical edge-case: treat as no collision
                        continue;
                    }

                    // Resolve along the smallest penetration axis
                    if (overlapX < overlapY)
                    {
                        // Resolve horizontally
                        if (position.x < center.x)
                        {
                            // Player is left of solid -> push left
                            //position.x -= overlapX;
                            position.x = sMin.x - halfW;
                        }
                        else
                        {
                            // Player is right of solid -> push right
                            //position.x += overlapX;
                            position.x = sMax.x + halfW;
                        }
                        hSpd = 0f;
                    }
                    else
                    {
                        // Resolve vertically
                        if (position.y < center.y)
                        {
                            // Player is below solid -> push down
                            //position.y -= overlapY;
                            position.y = sMin.y - playerHeight;
                            // If hitting underside, zero vertical speed
                            vSpd = 0f;
                        }
                        else
                        {
                            // Player is above solid -> land on top
                            //position.y += overlapY;
                            position.y = sMax.y;
                            vSpd = 0f;
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

            float halfW = playerWidth * 0.5f;
            float halfH = playerHeight * 0.5f;

            // Player AABB
            float pMinX = position.x + hSpd - halfW;
            float pMaxX = position.x + hSpd + halfW;
            float pMinY = position.y + vSpd;
            float pMaxY = position.y + vSpd + playerHeight;

            for (int i = 0; i < platformCount; i++)
            {
                Vector2 center = stageDataSO.platformCenter[i];
                Vector2 extent = stageDataSO.platformExtent[i];

                // Treat extent as half-extents: platform min/max
                Vector2 sMin = center - extent;
                Vector2 sMax = center + extent;

                // Quick horizontal rejection (platforms only matter when horizontally overlapping)
                if (pMaxX < sMin.x || pMinX > sMax.x)
                {
                    continue;
                }

                // Quick vertical rejection: platforms are thin surfaces; only consider collisions near the top surface.
                // We'll only allow collision when the player is at or above the platform top and moving downward (or stationary).
                // This implements a simple one-way platform behaviour.
                float platformTop = sMax.y;
                float platformBottom = sMin.y;

                // If player is completely below platform top, ignore.
                if (pMaxY <= sMin.y)
                    continue;

                // Overlap in X direction
                float overlapX = Mathf.Min(pMaxX, sMax.x) - Mathf.Max(pMinX, sMin.x);
                if (overlapX <= 0f)
                    continue;

                // If checkOnly is requested and player's AABB intersects platform horizontally and vertically area, report true.
                if (checkOnly)
                {
                    // Only report true for platforms when player is above or intersecting the top surface area
                    if (pMinY < platformTop && pMaxY > sMin.y)
                        return true;
                    continue;
                }

                // Only land on the platform when the player's bottom is at or above the platform top (or intersecting it)
                // and the player is moving downward (vSpd <= 0) or already essentially resting on it.
                // This avoids blocking the player from jumping up through the platform.
                if (pMinY <= platformTop && position.y >= platformTop && vSpd <= 0f)
                {
                    if ((input.ButtonStates[1] is ButtonState.Pressed or ButtonState.Held) && input.Direction == 2)
                    {
                        // Player is pressing down-jump while above platform: ignore collision (drop through)
                        return returnVal;
                    }
                    // Snap player to platform top
                    position.y = platformTop;
                    vSpd = 0f;
                    isGrounded = true;
                    onPlatform = true;
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

        // --- ACTIVATABLE SOLIDS (solids that have a bool on whether you check for their collision) ---
        if (stageDataSO.activatableSolidCenter != null && stageDataSO.activatableSolidExtent != null)
        {
            int activatableSolidCount = Mathf.Min(stageDataSO.activatableSolidCenter.Length, stageDataSO.activatableSolidExtent.Length);
            if (activatableSolidCount > 0)
            {
                float halfW = playerWidth * 0.5f;
                float halfH = playerHeight * 0.5f;

                // Player AABB
                float pMinX = position.x + hSpd - halfW;
                float pMaxX = position.x + hSpd + halfW;
                float pMinY = position.y + vSpd;
                float pMaxY = position.y + vSpd + playerHeight;

                //first get the activation status of the solid from the scene by finding the object in the scene with the tag "activatableSolid" and checking its active status
                GameObject[] activatableSolidsInScene = GameObject.FindGameObjectsWithTag("activatableSolid");

                for (int i = 0; i < activatableSolidCount; i++)
                {

                    

                    //find the activatable solid that corresponds to this index via matching the center position
                    bool isOpen = false;
                    foreach (GameObject obj in activatableSolidsInScene)
                    {
                        Vector3 objPos = obj.transform.position;
                        if (Mathf.Approximately(objPos.x, stageDataSO.activatableSolidCenter[i].x) &&
                            Mathf.Approximately(objPos.y, stageDataSO.activatableSolidCenter[i].y))
                        {
                            isOpen = obj.GetComponent<SpellCode_Gate>().isOpen;
                            break;
                        }
                    }
                    if (!isOpen)
                    {
                        Vector2 center = stageDataSO.activatableSolidCenter[i];
                        Vector2 extent = stageDataSO.activatableSolidExtent[i];

                        // Treat extent as half-extents: solid min/max
                        Vector2 sMin = center - extent;
                        Vector2 sMax = center + extent;

                        // Quick rejection test
                        if (pMaxX < sMin.x || pMinX > sMax.x || pMaxY < sMin.y || pMinY > sMax.y)
                        {
                            continue;
                        }

                        // Overlap detected
                        if (checkOnly)
                        {
                            return true;
                        }

                        // Compute penetration amounts
                        float overlapX = Mathf.Min(pMaxX, sMax.x) - Mathf.Max(pMinX, sMin.x);
                        float overlapY = Mathf.Min(pMaxY, sMax.y) - Mathf.Max(pMinY, sMin.y);

                        if (overlapX < 0f || overlapY < 0f)
                        {
                            // Numerical edge-case: treat as no collision
                            continue;
                        }

                        // Resolve along the smallest penetration axis
                        if (overlapX < overlapY)
                        {
                            // Resolve horizontally
                            if (position.x < center.x)
                            {
                                // Player is left of solid -> push left
                                //position.x -= overlapX;
                                position.x = sMin.x - halfW;
                            }
                            else
                            {
                                // Player is right of solid -> push right
                                //position.x += overlapX;
                                position.x = sMax.x + halfW;
                            }
                            hSpd = 0f;
                        }
                        else
                        {
                            // Resolve vertically
                            if (position.y < center.y)
                            {
                                // Player is below solid -> push down
                                //position.y -= overlapY;
                                position.y = sMin.y - playerHeight;
                                // If hitting underside, zero vertical speed
                                vSpd = 0f;
                            }
                            else
                            {
                                // Player is above solid -> land on top
                                //position.y += overlapY;
                                position.y = sMax.y;
                                vSpd = 0f;
                                isGrounded = true;
                            }
                        }

                        returnVal = true;
                    }




                    
                }
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
                //playerHeight = charData.playerHeight / 2;

                break;
            case PlayerState.Hitstun:
                ClearInputDisplay();
                stateSpecificArg = hitboxData.hitstun;
                hSpd = hitboxData.xKnockback * (facingRight ? -1 : 1);
                vSpd = hitboxData.yKnockback;

                //if (isGrounded)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    isGrounded = false;
                //}

                break;

            case PlayerState.Tech:
                hSpd = facingRight ? -1 : 1;
                vSpd = 5;
                //if (isGrounded)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    isGrounded = false;
                //}
                comboCounter = 0;
                damageProration = 1f;
                break;
            case PlayerState.CodeWeave:
                //play codeweave sound
                if (/*vSpd < 0 && */!isGrounded)
                {
                    vSpd = 0;
                    gravity = 0;
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



    /// <summary>
    /// this function lerps the horizontal speed towards a target value over time deterministically
    /// </summary>
    /// <param name="targetHspd"></param>
    /// <param name="lerpval"></param>
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
        bw.Write(gravity);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write(onPlatform);
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
        bw.Write(flowState);
        bw.Write(stockStability);
        bw.Write(demonAura);
        bw.Write(reps);
        bw.Write(momentum);
        bw.Write(slimed);

        //bw.Write(InputConverter.ConvertFromInputSnapshot(bufferInput));
    }


    public void Deserialize(BinaryReader br)
    {
        position.x = br.ReadSingle();
        position.y = br.ReadSingle();
        hSpd = br.ReadSingle();
        vSpd = br.ReadSingle();
        gravity = br.ReadSingle();
        facingRight = br.ReadBoolean();
        isGrounded = br.ReadBoolean();
        onPlatform = br.ReadBoolean();
        state = (PlayerState)br.ReadByte();
        prevState = (PlayerState)br.ReadByte();
        logicFrame = br.ReadInt32();
        lerpDelay = br.ReadUInt16();
        stateSpecificArg = br.ReadUInt32();
        hitstop = br.ReadByte();
        //hitboxActive = br.ReadBoolean();
        hitstopActive = br.ReadBoolean();
        hitstunOverride = br.ReadBoolean();
        comboCounter = br.ReadUInt16();
        flowState = br.ReadUInt16();
        stockStability = br.ReadUInt16();
        demonAura = br.ReadUInt16();
        reps = br.ReadUInt16();
        momentum = br.ReadUInt16();
        slimed = br.ReadBoolean();
        //bufferInput = InputConverter.ConvertFromShort(br.ReadInt16());
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

    public void ClearInputDisplay()
    {
        inputDisplay.text = "";
        inputDisplay.color = Color.white;
    }

    public static string ConvertCodeToString(uint code, Color ?color = null)
    {
        if (color == null) { color = Color.white; }

        string codeString = "";
        byte codeCount = (byte)(code & 0xF); //get the last 4 bits of stateSpecificArg
        for (int i = 0; i < codeCount; i++)
        {
            byte currentInput = (byte)((code >> (8 + (i * 2))) & 0b11);
            switch (currentInput)
            {
                case 0b00:
                    codeString += "<sprite name=\"ArrowDown\" tint=\"1\"> ";
                    break;
                case 0b01:
                    codeString += "<sprite name=\"ArrowRight\" tint=\"1\"> ";
                    break;
                case 0b10:
                    codeString += "<sprite name=\"ArrowLeft\" tint=\"1\"> ";
                    break;
                case 0b11:
                    codeString += "<sprite name=\"ArrowUp\" tint=\"1\"> ";
                    break;
            }
        }
        return codeString.Trim();
    }
}





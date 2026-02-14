using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
//using UnityEditor.Experimental.GraphView;

//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
// Alias for convenience (optional, but recommended for readability)
using Fixed = BestoNet.Types.Fixed32;
using FixedVec2 = BestoNet.Types.Vector2<BestoNet.Types.Fixed32>;
using FixedVec3 = BestoNet.Types.Vector3<BestoNet.Types.Fixed32>;

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
    public string characterName = "R-Cade";



    private ushort lerpDelay = 0;
    [NonSerialized]
    public FixedVec2 position;

    public bool facingRight = true;
    public bool isGrounded = false;
    public bool onPlatform = false;

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
    public Fixed gravity = Fixed.FromFloat(0.75f);
    [HideInInspector]
    public Fixed jumpForce = Fixed.FromInt(10);
    public Fixed runSpeed = Fixed.FromInt(0);
    public Fixed slideSpeed = Fixed.FromInt(0);

    //Spell Resource Variables
    public ushort flowState = 0; //the timer for how long you are in flow state
    public const ushort maxFlowState = 600;
    public ushort stockStability = 0; //percentage chance to crit, e.g. 25 = 25% chance
    public ushort demonAura = 0;
    public const ushort maxDemonAura = 100;
    public ushort demonAuraLifeSpanTimer = 0;
    public ushort reps = 0;
    //public ushort momentum = 0;
    //public bool slimed = false;



    //MATCH STATS
    public Texture2D[] matchPalette = new Texture2D[2];
    public ushort currentPlayerHealth = 0;

    //money things
    [NonSerialized]
    public ushort totalRam = 0;
    [NonSerialized]
    public ushort roundRam = 0;
    [NonSerialized]
    public short ramBounty = 0;
    [NonSerialized]
    public const ushort baseRamKillBonus = 100;
    [NonSerialized]
    public const ushort baseRamLifeWorth = 200;

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

    public uint storedCode = 0; //the code that is stored up for release
    public uint storedCodeDuration = 0; //how many more logic frames the stored code will last before auto-releasing

    public byte hitstop = 0;
    public bool hitstopActive = false;
    public bool hitstunOverride = false;
    public bool lightArmor = false;

    public List<SpellData> spellList = new List<SpellData>();
    public GameObject basicProjectileInstance;

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
    public Fixed timer = Fixed.FromInt(0);
    //public bool timerRunning = false;
    public List<Fixed> times = new List<Fixed>();

    public int roundsWon;

    public bool chosenSpell = false;
    public bool chosenStartingSpell = false;
    public bool isSpawned;
    public string startingSpell;

    public int pID;

    //these variables are to track what collectives the player has. Passives for each collective
    //will only show up if the boolean is true
    public bool vWave = false;
    public bool killeez = false;
    public bool DemonX = false;
    public bool bigStox = false;


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
        runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
        slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
        jumpForce = Fixed.FromInt(charData.jumpForce);
        playerWidth = Fixed.FromInt(charData.playerWidth);
        playerHeight = Fixed.FromInt(charData.playerHeight);

        //startingSpell = charData.startingInventory[0];

        //fill the spell list with the character's initial spells
        //for (int i = 0; i < charData.startingInventory.Count /*&& i < spellList.Count*/; i++)
        //{
        //SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
        //spellList.Add = Instantiate(targetSpell);
        //spellList[i].owner = this;
        //spellCount++;

        //AddSpellToSpellList(charData.startingInventory[i]);
        //}

        //temp palette assignment based on player index
        switch (Array.IndexOf(GameManager.Instance.players, this))
        {
            case 0:
                InitializePalette(matchPalette[0]);
                //playerNum.text = "P1";
                pID = 1;
                playerNum.color = Color.red;
                break;
            case 1:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P2";
                pID = 2;
                playerNum.color = Color.cyan;
                gameObject.GetComponent<SpriteRenderer>().color = Color.cyan;
                break;
            case 2:
                InitializePalette(matchPalette[0]);
                //playerNum.text = "P3";
                pID = 3;
                playerNum.color = Color.yellow;
                gameObject.GetComponent<SpriteRenderer>().color = Color.yellow;
                break;
            case 3:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P4";
                pID = 4;
                playerNum.color = Color.green;
                gameObject.GetComponent<SpriteRenderer>().color = Color.green;
                break;
        }

        //DELETE THIS LATER, JUST TO LOCK STARTING SPELL TO PID
        if (pID == 1) { startingSpell = "PongShot"; }
        else if (pID == 2) { startingSpell = "QuarterReport"; }
        else if (pID == 3) { startingSpell = "BladeOfAres"; }
        else if (pID == 4) { startingSpell = "SkillshotSlash"; }

            FixedVec2 startPos;
        Vector2 spawnPos = GameManager.Instance.GetSpawnPositions()[Array.IndexOf(GameManager.Instance.players, this)];
        startPos = FixedVec2.FromFloat(spawnPos.x, spawnPos.y);
        SpawnPlayer(startPos);


        //Vector3 spawnPosV3 = GameManager.Instance.stages[GameManager.Instance.currentStageIndex].playerSpawnTransform[Array.IndexOf(GameManager.Instance.players, this)];
        //startPos = FixedVec2.FromFloat(spawnPosV3.x, spawnPosV3.y);
        //SpawnPlayer(startPos);

        //ProjectileManager.Instance.InitializeAllProjectiles();
    }

    public void SpawnPlayer(FixedVec2 spawnPos)
    {
        isAlive = true;
        gameObject.GetComponent<SpriteRenderer>().enabled = true;
        position = spawnPos;
        hSpd = Fixed.FromInt(0);
        vSpd = Fixed.FromInt(0);
        stateSpecificArg = 0;
        currentPlayerHealth = charData.playerHealth;
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
        //momentum = 0;
        //slimed = false;

        //call the load spell function for the starting spell to initialize the spell's variables and projectile data
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null)
            {
                spellList[i].owner = this;
                spellList[i].LoadSpell();
            }
        }
        GameManager.Instance.tempSpellDisplays[Array.IndexOf(GameManager.Instance.players, this)].UpdateSpellDisplay(Array.IndexOf(GameManager.Instance.players, this));

        //ProjectileManager.Instance.InitializeAllProjectiles();

    }


    

    public void AddSpellToSpellList(string spellToAdd)
    {
        if (spellList.Count >= 6)
        {
            Debug.LogWarning("Spell List Full, cannot add more spells!");
            return;
        }
        SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[spellToAdd];
        spellList.Add(Instantiate(targetSpell));
        spellList[spellList.Count - 1].owner = this;
        spellList[spellList.Count - 1].LoadSpell();
        ProjectileManager.Instance.InitializeAllProjectiles();

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);

        //trigger bools depending on brand
        for (int i = 0; i < targetSpell.brands.Length; i++)
        {
            if (targetSpell.brands[i] == Brand.VWave && vWave == false)
            {
                vWave = true;
                Debug.Log("Player has unlocked VWave passives");
            }
            if (targetSpell.brands[i] == Brand.Killeez && killeez == false)
            {
                killeez = true;
                Debug.Log("Player has unlocked Killeez passives");
            }
            if (targetSpell.brands[i] == Brand.DemonX && DemonX == false)
            {
                DemonX = true;
                Debug.Log("Player has unlocked DemonX passives");
            }
            if (targetSpell.brands[i] == Brand.BigStox && bigStox == false)
            {
                bigStox = true;
                Debug.Log("Player has unlocked BigStox passives");
            }
        }
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
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null && spellList[i].spellName == spellToRemove)
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
        //for (int i = 0; i < charData.startingInventory.Count /*&& i < spellList.Count*/; i++)
        //{
        //SpellData targetSpell = (SpellData)SpellDictionary.Instance.spellDict[charData.startingInventory[i]];
        //spellList.Add = Instantiate(targetSpell);
        //spellList[i].owner = this;
        //spellCount++;

        // AddSpellToSpellList(charData.startingInventory[i]);
        //}

        AddSpellToSpellList(startingSpell);

        roundsWon = 0;


        //data
        spellsFired = 0;
        basicsFired = 0;
        spellsHit = 0;
        timer = Fixed.FromInt(0);
        times = new List<Fixed>();

        //passive resources
        flowState = 0;
        stockStability = 0;
        demonAura = 0;
        reps = 0;
        //momentum = 0;
        //slimed = false;
        storedCode = 0;

        vWave = false;
        killeez = false;
        DemonX = false;
        bigStox = false;

    currentPlayerHealth = 100;
        isAlive = true;
    }


    /// MOVEMENT CODE
    public ulong GetInputs()
    {
        //Debug.Log($"[GetInputs] Called on player at index {System.Array.IndexOf(GameManager.Instance.players, this)}, " +
        //      $"IsActive={inputs.IsActive}, " +
        //      $"IsOnlineMatch={GameManager.Instance.isOnlineMatchActive}");


        ulong input = 0;

        // In online mode, only the local player gathers input
        if (GameManager.Instance.isOnlineMatchActive)
        {
            int myIndex = System.Array.IndexOf(GameManager.Instance.players, this);

            // Only gather input if this is the local player
            if (myIndex == GameManager.Instance.localPlayerIndex)
            {
                //input = GetRawKeyboardInput(); // Old Input API method
                long longInput = inputs.UpdateInputs();
                input = (ulong)longInput; // Input System method
                return input;
            }
            else
            {
                return 0; // Remote player, return neutral
            }
        }

        // Use Input System for both online and offline
        if (inputs.IsActive)
        {
            long longInput = inputs.UpdateInputs();
            input = (ulong)longInput;
        }

        return input;
    }

    /// <summary>
    /// Gets keyboard input directly using Unity's old Input API.
    /// This bypasses the Input System entirely.
    /// </summary>
    private ulong GetRawKeyboardInput()
    {
        // DEBUG: Check if ANY key is being pressed
        if (UnityEngine.Input.anyKey)
        {
            Debug.LogWarning($"[GetRawKeyboardInput] SOME KEY IS PRESSED!");
            // Log specific keys
            Debug.LogWarning($"W={UnityEngine.Input.GetKey(KeyCode.W)}, " +
                            $"A={UnityEngine.Input.GetKey(KeyCode.A)}, " +
                            $"S={UnityEngine.Input.GetKey(KeyCode.S)}, " +
                            $"D={UnityEngine.Input.GetKey(KeyCode.D)}");
        }

        // Direction input (using numpad notation: 5 = neutral)
        bool up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
        bool down = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
        bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
        bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

        // Button states (need to track previous state for Pressed/Released detection)
        bool codeNow = UnityEngine.Input.GetKey(KeyCode.R);
        bool jumpNow = UnityEngine.Input.GetKey(KeyCode.T);

        // Store previous button states (you might need to add these as class fields)
        bool codePrev = codePrevFrame;
        bool jumpPrev = jumpPrevFrame;

        // Update for next frame
        codePrevFrame = codeNow;
        jumpPrevFrame = jumpNow;

        // Calculate direction (numpad notation)
        byte direction = 5; // neutral

        if (up && right) direction = 9;
        else if (up && left) direction = 7;
        else if (down && right) direction = 3;
        else if (down && left) direction = 1;
        else if (up) direction = 8;
        else if (down) direction = 2;
        else if (left) direction = 4;
        else if (right) direction = 6;

        // Calculate button states
        ButtonState codeState = GetButtonState(codePrev, codeNow);
        ButtonState jumpState = GetButtonState(jumpPrev, jumpNow);

        ButtonState[] buttons = new ButtonState[2] { codeState, jumpState };
        bool[] dirs = new bool[4] { up, down, left, right };

        Debug.Log($"[GetRawKeyboardInput] Direction={direction}, Code={codeState}, Jump={jumpState}");

        // Convert to ulong using your existing converter
        return (ulong)InputConverter.ConvertToLong(buttons, dirs);
    }

    private bool codePrevFrame = false;
    private bool jumpPrevFrame = false;

    private ButtonState GetButtonState(bool previous, bool current)
    {
        if (!previous && !current)
            return ButtonState.None;
        else if (current && !previous)
            return ButtonState.Pressed;
        else if (current && previous)
            return ButtonState.Held;
        else
            return ButtonState.Released;
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
            //reset cooldowns of all spells in spell list so that they are ready to be used when the player respawns
            foreach (SpellData spell in spellList)
            {
                if (spell != null)
                {
                    spell.cooldownCounter = 0;
                }
            }
            isAlive = false;
            return;
        }

        input = InputConverter.ConvertFromLong((ulong)rawInput);


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

        //check for releasing a stored code
        CheckReleaseCode(input);



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
                if (input.Direction < 4 && input.ButtonStates[1] == ButtonState.Pressed)
                {
                    if (input.Direction == 2 && onPlatform)
                    {
                        break;
                    }
                    SetState(PlayerState.Slide);
                    break;
                }
                //Check Direction Inputs
                if (input.Direction % 3 == 0) //3 6 or 9
                {
                    facingRight = true;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.Direction % 3 == 1)// 1 4 or 7
                {
                    facingRight = false;
                    SetState(PlayerState.Run);
                    break;
                }
                else if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    //play the enter weave sound
                    SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                    SetState(PlayerState.CodeWeave);
                    break;
                }
                else if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    vSpd = jumpForce;

                    //play the jump sound
                    SFX_Manager.Instance.PlaySound(Sounds.JUMP);

                    SetState(PlayerState.Jump);
                    break;
                }
                LerpHspd(Fixed.FromInt(0), 3);
                break;
            case PlayerState.Run:

                //...
                if (logicFrame % CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Run) == 0 || logicFrame % CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Run) == CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Run).frameLengths.Take(3).Sum() + 1)
                {
                    //play the run sound
                    SFX_Manager.Instance.PlaySound(Sounds.RUN);
                }

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


                if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    //play the enter weave sound
                    SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                    SetState(PlayerState.CodeWeave);
                    break;
                }
                else if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    vSpd = jumpForce;

                    //play the jump sound
                    SFX_Manager.Instance.PlaySound(Sounds.JUMP);

                    SetState(PlayerState.Jump);
                    break;
                }
                else if (input.Direction % 3 == (facingRight ? 1 : 0))
                {
                    facingRight = !facingRight;
                    break;
                }
                else if (input.Direction % 3 == (facingRight ? 0 : 1))
                {
                    //run logic
                    LerpHspd(runSpeed * (facingRight ? Fixed.FromInt(1) : Fixed.FromInt(-1)), 1);
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
                if (vSpd > Fixed.FromInt(0) && input.ButtonStates[1] is ButtonState.Released or ButtonState.None)
                {
                    //reapply gravity more strongly to create a variable jump height
                    vSpd -= gravity * Fixed.FromInt(2);
                }
                if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    //play the enter weave sound
                    SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                    SetState(PlayerState.CodeWeave);
                    break;
                }

                //check for slide input:
                if (input.Direction < 4 && input.ButtonStates[1] == ButtonState.Pressed)
                {
                    SetState(PlayerState.Slide);
                    break;
                }

                if (input.Direction % 3 == 0)
                {
                    //run logic
                    facingRight = true;
                    LerpHspd(runSpeed, 3);
                }
                else if (input.Direction % 3 == 1)
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


                if (codeCount < 12 && ((stateSpecificArg & (1u << 4)) != 0 || (currentInput != lastInputInQueue && stateSpecificArg != 0))) //if the 5th bit is a 1, and we have a valid direction input, we can record it
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
                            //play the input code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE, 0.95f, 0.95f);
                            break;
                        case 4:
                            stateSpecificArg |= (uint)(0b10 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("left input Pressed!");
                            //play the input code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE, 1.05f, 1.05f);
                            break;
                        case 6:
                            stateSpecificArg |= (uint)(0b01 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("right input Pressed!");
                            //play the input code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE, 1f, 1f);
                            break;
                        case 8:
                            stateSpecificArg |= (uint)(0b11 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("up input Pressed!");
                            //play the input code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE, 1.1f, 1.1f);
                            break;
                        default:
                            //stateSpecificArg &= ~(1u << 4);
                            break;
                    }

                    // Increment the last 4 bits of stateSpecificArg by 1
                    if ((stateSpecificArg & (1u << 4)) == 0)
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
                    //Debug.Log($"your inputted code: {Convert.ToString(stateSpecificArg, toBase: 2)}");

                    lightArmor = false;
                    for (int i = i = 0; i < spellList.Count; i++)
                    {
                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter <= 0)
                        {
                            Debug.Log($"You Released {spellList[i].spellName}!");
                            lightArmor = true;
                            break;
                        }
                    }

                    SetState(PlayerState.CodeRelease, stateSpecificArg);

                    break;
                }

                //jump button pressed
                if (input.ButtonStates[1] == ButtonState.Pressed)
                {
                    lightArmor = false;
                    //set the 5th bit to 0 to indicate we are no longer primed
                    stateSpecificArg &= ~(1u << 4);
                    //if the current code is a valid spell code, store it for later use
                    for (int i = 0; i < spellList.Count; i++)
                    {
                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter <= 0)
                        {

                            storedCode = stateSpecificArg;

                            uint spellCodeLength = (storedCode & 0xFu);
                            storedCodeDuration = Math.Clamp(5 - spellCodeLength, 0, 5) * 60; //stored code lasts for 5 seconds (300 logic frames) minus 1 second (60 logic frames) per input in the code
                            SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                            break;
                        }
                    }
                    //If the code is not valid, clear the input display and reset the stored code
                    if (storedCode == 0)
                    {

                        ClearInputDisplay();
                        stateSpecificArg = 0;
                    }
                }


                LerpHspd(Fixed.FromInt(0), isGrounded ? 3 : 15);
                break;
            case PlayerState.CodeRelease:
                //allow the display to be reset upon entering CodeWeave state
                removeInputDisplay = true;

                if (input.Direction == 6)
                {
                    facingRight = true;
                }
                else if (input.Direction == 4)
                {
                    facingRight = false;
                }



                if (logicFrame == charData.animFrames.codeReleaseAnimFrames.frameLengths.Take(3).Sum())
                {
                    for (int i = 0; i < spellList.Count; i++)
                    {
                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter <= 0)
                        {
                            Debug.Log($"You Cast {spellList[i].spellName}!");
                            spellList[i].activateFlag = true;
                            spellList[i].CheckCondition(null, ProcCondition.ActiveOnCast);

                            //keep track of how long player is in state for
                            times.Add(timer);
                            timer = Fixed.FromInt(0);

                            //set stateSpecificArg to 255 as it is a value we can never normally set it to, to indicate that we successfully fired a spell
                            stateSpecificArg = 255;

                            //spellcode is fired
                            spellsFired++;

                            //make input display flash green to indicate correct input sequence
                            inputDisplay.color = Color.green;

                            //play successful code cast sound
                            SFX_Manager.Instance.PlaySound(Sounds.EXIT_CODE_WEAVE);

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

                    // Check conditions of all spells with the onCast condition
                    CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnCast);


                    //check if we set stateSpecificArg to 255, which is otherwise impossible to achieve, in the spell loop, meaning we successfully fired a spell
                    if (stateSpecificArg == 255)
                    {
                        //so check all spells with OnCastSpell condition

                        CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnCastSpell);
                        break;
                    }
                    CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnCastBasic);
                    //create an instance of your basic spell here
                    BaseProjectile newProjectile = (BaseProjectile)ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                    ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new FixedVec2(Fixed.FromInt(16), Fixed.FromInt(36)));


                    //basic spell is fired
                    basicsFired++;

                    //make input display flash red to indicate incorrect sequence

                    if (stateSpecificArg != 0)
                    {
                        //Play failed code weave sound
                        SFX_Manager.Instance.PlaySound(Sounds.FAILED_EXIT_CODE_WEAVE);
                    }
                    else if(stateSpecificArg == 0)
                    {
                        //play successful code cast sound
                        SFX_Manager.Instance.PlaySound(Sounds.EXIT_CODE_WEAVE);
                    }
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.CodeRelease))
                {
                    if (input.ButtonStates[0] == ButtonState.Held)
                    {
                        SetState(PlayerState.CodeWeave);
                        break;
                    }
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
                if (isGrounded)
                {
                    LerpHspd(Fixed.FromInt(0), 3);
                }

                stateSpecificArg--;
                break;
            case PlayerState.Tech:
                if (isGrounded)
                {
                    SetState(input.ButtonStates[0] == ButtonState.Held ? PlayerState.CodeWeave : PlayerState.Idle);
                    break;
                }

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Tech))
                {
                    if(input.ButtonStates[0] == ButtonState.Held)
                    {
                        SetState(PlayerState.CodeWeave);
                        break;
                    }
                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }

                break;
            case PlayerState.Slide:
                if (!isGrounded)
                {
                    vSpd = Fixed.FromInt(-2);
                }
                else if (input.ButtonStates[1] == ButtonState.Pressed)   //jump out of slide only on the ground
                {
                    vSpd = jumpForce;
                    SetState(PlayerState.Jump);
                    break;
                }

                //check for slide end frame to trigger onSlide spell conditions
                if (logicFrame == CharacterDataDictionary.GetAnimFrames(characterName, PlayerState.Slide).frameLengths.Take(2).Sum() + 1)
                {
                    // Check conditions of all spells with the onSlide condition
                    CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnSlide);
                }


                if (input.ButtonStates[0] == ButtonState.Pressed)
                {
                    //play the enter weave sound
                    SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                    SetState(PlayerState.CodeWeave);
                    break;
                }
                LerpHspd(Fixed.FromInt(0), charData.slideFriction);

                if (logicFrame >= CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Slide))
                {

                    SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                    break;
                }
                break;
        }


        //Check conditions of all spells with the onupdate condition
        CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnUpdate);

        if (demonAura > 0)
        {
            if (demonAuraLifeSpanTimer > 0)
            {
                demonAuraLifeSpanTimer--;
            }
            else
            {
                demonAura = (ushort)Math.Clamp(demonAura - 1, 0, maxDemonAura);
            }

        }
        UpdateResources();

        //check player collisions
        PlayerWorldCollisionCheck();

        position += new FixedVec2(hSpd, vSpd);
        //position.x += hSpd;
        //position.y += vSpd;

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
                    tempColor = Color.red;
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
            switch (Array.IndexOf(GameManager.Instance.players, this))
            {
                case 0:
                    playerSpriteRenderer.color = Color.red;
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
        StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? GameManager.Instance.lobbySO : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
        if (stageDataSO == null || stageDataSO.solidCenter == null || stageDataSO.solidExtent == null)
        {
            // if there's no stage or no solids at all, still check platforms below (handled later)
            if (stageDataSO == null) return false;
        }

#region  --- SOLIDS (unchanged behavior) ---
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

                    if (overlapX < Fixed.FromInt(0) || overlapY < Fixed.FromInt(0))
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
                            position = new FixedVec2(sMax.X + halfW, position.Y);
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
        #endregion
#region --- PLATFORMS (one-way: only collide from above while falling/standing) ---
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
                if (pMinY <= platformTop && position.Y >= platformTop && vSpd <= Fixed.FromInt(0))
                {
                    if ((input.ButtonStates[1] is ButtonState.Pressed or ButtonState.Held) && input.Direction == 2)
                    {
                        // Player is pressing down-jump while above platform: ignore collision (drop through)
                        return returnVal;
                    }
                    // Snap player to platform top
                    position = new FixedVec2(position.X, platformTop);
                    vSpd = Fixed.FromInt(0);
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
        #endregion
#region--- ACTIVATABLE SOLIDS (solids that have a bool on whether you check for their collision) ---
        if (stageDataSO.activatableSolidCenter != null && stageDataSO.activatableSolidExtent != null)
        {
            int activatableSolidCount = Mathf.Min(stageDataSO.activatableSolidCenter.Length, stageDataSO.activatableSolidExtent.Length);
            if (activatableSolidCount > 0)
            {
                Fixed halfW = playerWidth * Fixed.FromFloat(0.5f);
                Fixed halfH = playerHeight * Fixed.FromFloat(0.5f);

                // Player AABB
                Fixed pMinX = position.X + hSpd - halfW;
                Fixed pMaxX = position.X + hSpd + halfW;
                Fixed pMinY = position.Y + vSpd;
                Fixed pMaxY = position.Y + vSpd + playerHeight;

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

                        FixedVec2 center = FixedVec2.FromFloat(stageDataSO.activatableSolidCenter[i].x, stageDataSO.activatableSolidCenter[i].y);
                        FixedVec2 extent = FixedVec2.FromFloat(stageDataSO.activatableSolidExtent[i].x, stageDataSO.activatableSolidExtent[i].y);

                        // Treat extent as half-extents: platform min/max
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

                        if (overlapX < Fixed.FromInt(0) || overlapY < Fixed.FromInt(0))
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
                                position = new FixedVec2(sMax.X + halfW, position.Y);
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
        }
        #endregion
        return returnVal;
    }

    

    public void SetState(PlayerState targetState, uint inputSpellArg = 0)
    {


        prevState = state;
        HandleExitLogic(prevState);
        state = targetState;
        HandleEnterState(targetState, inputSpellArg);
        hitstunOverride = false;
    }



    //move logic for each state here
    private void HandleEnterState(PlayerState curstate, uint inputSpellArg)
    {


        logicFrame = 0;
        animationFrame = 0;
        //float knockbackMultiplier = 0;
        switch (curstate)
        {
            case PlayerState.Idle:
                hitboxData = null;
                break;
            case PlayerState.Run:
                //play the dash dust VFX
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DASH_DUST, position, pID, facingRight);
                break;
            case PlayerState.Jump:
                //playerHeight = charData.playerHeight / 2;

                break;
            case PlayerState.Hitstun:
                ClearInputDisplay();

                //reset storedCode if you get hit
                storedCode = 0;
                storedCodeDuration = 0;

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
                break;
            case PlayerState.CodeWeave:
                lightArmor = true;
                //play codeweave sound
                SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                //begin to continuously play the code weave sound
                SFX_Manager.Instance.StartRepeatingSound(Sounds.CONTINUOUS_CODE_WEAVE, 0.42f, Array.IndexOf(GameManager.Instance.players, this), 1f, 1f);

                if (!isGrounded)
                {
                    vSpd = Fixed.FromInt(0);
                    gravity = Fixed.FromInt(0);
                }

                //update the player's spell display to show the spell inputs
                int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
                GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex, true);

                break;
            case PlayerState.CodeRelease:

                stateSpecificArg = storedCode != 0 ? storedCode : inputSpellArg;

                //reset stored code after using it
                storedCode = 0;
                storedCodeDuration = 0;
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
                gravity = Fixed.FromFloat(.75f);
                break;
            case PlayerState.CodeRelease:
                //begin to continuously play the code weave sound
                SFX_Manager.Instance.StopRepeatingSound(Sounds.CONTINUOUS_CODE_WEAVE, Array.IndexOf(GameManager.Instance.players, this));

                //turn off hitstun override when exiting code release in case we exited code release while still having hitstun override on from casting a spell
                lightArmor = false;
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
    public void TakeEffectDamage(int damageAmount, PlayerController attacker)
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
        GameManager.Instance.damageMatrix[pID - 1, attacker.pID - 1] += (byte)Mathf.Clamp(damageAmount, 0, currentPlayerHealth);

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

            // isHit = false;

            //ignore hit if we are in codeweave and the attack level is less than 2 (basic attack)
            if (lightArmor && hitboxData.attackLvl < 2)
            {
                return;
            }

            //mySFXHandler.PlaySound(SoundType.DAMAGED);


            //update the damage matrix the attacker attacking this player
            GameManager.Instance.damageMatrix[pID - 1, attacker.pID - 1] += (byte)Mathf.Clamp(hitboxData.damage, 0, currentPlayerHealth);

            //checking for death
            if (hitboxData.damage >= currentPlayerHealth)
            {
                //play the death sound
                SFX_Manager.Instance.PlaySound(Sounds.DEATH);

                
                currentPlayerHealth = 0;

                //award the killer with the extra bonus ram
                attacker.roundRam += baseRamKillBonus;
                attacker.totalRam += baseRamKillBonus;
            }
            else
            {


                // Reduce health 
                currentPlayerHealth = (ushort)(currentPlayerHealth - (int)hitboxData.damage);

            }


            //GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));

            //play the damaged sound
            SFX_Manager.Instance.PlaySound(Sounds.HIT);

            SetState(PlayerState.Hitstun);

            //call the active on hit proc of the spell that created the projectile that hit us
            if (hitboxData.parentProjectile.ownerSpell != null)
            {
                hitboxData.parentProjectile.ownerSpell.CheckCondition(this, ProcCondition.ActiveOnHit);
            }


            //call the checkProcEffect call of every spell that has ProcEffect.OnHit in the attacker's spell list
            CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHit);
            

            //now call the checkProcEffect call of every spell that has ProcEffect.OnHurt in this player's spell list
            CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurt);

            //now check for OnHitBasic or OnHitSpell depending on whether the hitbox was a basic attack hitbox
            if (hitboxData.basicAttackHitbox)
            {
                CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHitBasic);
                CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurtBasic);
            }
            else
            {
                CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHitSpell);
                CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurtSpell);

                if(attacker.demonAura > 0)
                {
                    attacker.demonAuraLifeSpanTimer = 360; //refresh demon aura lifespan timer on spell hit to 6 seconds (360 frames)
                }
            }

            //subtract demon aura based on the hitbox's damage
            //demonAura = (ushort)Math.Max(0, demonAura - (int)hitboxData.damage);


        }
    }


    /// <summary>
    /// This is a Helper function that checks all spells in the target player's spell list for the specified ProcCondition and calls their CheckCondition method.
    /// </summary>
    /// <param name="targetPlayer"></param>
    /// <param name="targetProcCon"></param>
    public void CheckAllSpellConditionsOfProcCon(PlayerController targetPlayer, ProcCondition targetProcCon)
    {
        for (int i = 0; i < targetPlayer.spellList.Count; i++)
        {
            if (targetPlayer.spellList[i].procConditions.Contains(targetProcCon))
            {
                targetPlayer.spellList[i].CheckCondition(this, targetProcCon);
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
        bw.Write(timer.RawValue);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write(onPlatform);
        bw.Write((byte)state);
        bw.Write((byte)prevState);
        bw.Write(logicFrame); // 🔹 Save current animation frame
        bw.Write(animationFrame);
        bw.Write(lerpDelay);
        bw.Write(stateSpecificArg);
        bw.Write(hitstop);
        //bw.Write(hitboxActive);
        bw.Write(hitstopActive);
        bw.Write(hitstunOverride);
        bw.Write(lightArmor);
        bw.Write(storedCode);
        bw.Write(storedCodeDuration);
        bw.Write(currentPlayerHealth);
        bw.Write(isAlive);
        bw.Write(flowState);
        bw.Write(stockStability);
        bw.Write(demonAura);
        bw.Write(demonAuraLifeSpanTimer);
        bw.Write(reps);
        //bw.Write(momentum);
        //bw.Write(slimed);

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
        timer = new Fixed(br.ReadInt32());
        facingRight = br.ReadBoolean();
        isGrounded = br.ReadBoolean();
        onPlatform = br.ReadBoolean();
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
        lightArmor = br.ReadBoolean();
        storedCode = br.ReadUInt32();
        storedCodeDuration = br.ReadUInt16();
        currentPlayerHealth = br.ReadUInt16();
        isAlive = br.ReadBoolean();
        flowState = br.ReadUInt16();
        stockStability = br.ReadUInt16();
        demonAura = br.ReadUInt16();
        demonAuraLifeSpanTimer = br.ReadUInt16();
        reps = br.ReadUInt16();
        //momentum = br.ReadUInt16();
        //slimed = br.ReadBoolean();
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
            spellList[i].SpellUpdate();
        }
    }
    public bool IsStorableState() =>
        state == PlayerState.Idle ||
        state == PlayerState.Run ||
        state == PlayerState.Jump ||
        state == PlayerState.Slide ||
        state == PlayerState.CodeWeave;


    public void CheckReleaseCode(InputSnapshot targetInput)
    {
        if (storedCode == 0)
        {
            return;
        }

        if (storedCodeDuration > 0)
        {
            storedCodeDuration--;
            Debug.Log($"Stored code duration: {storedCodeDuration}");
        }

        if (targetInput.ButtonStates[0] == ButtonState.Released || storedCodeDuration <= 0)
        {

            if (IsStorableState())
            {
                //this is to keep the physics interactions between releasing a stored code and a normal code consistent, improving player experience
                vSpd = Fixed.FromInt(0);
                SetState(PlayerState.CodeRelease);
            }
        }
    }

    //private int GetPlayerIndex() =>
    //    Array.IndexOf(GameSessionManager.Instance.playerControllers, this);
    public void CheckForInputs(bool enable)
    {
        inputs.CheckForInputs(enable);
    }

    //public void UpdateInputDisplay(int direction)
    //{
    //    if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
    //    {
    //        //down
    //        if (direction == 2)
    //        {
    //            inputDisplay.text += "DOWN, ";
    //        }

    //        //left
    //        if (direction == 4)
    //        {
    //            inputDisplay.text += "LEFT,  ";
    //        }

    //        //right
    //        if (direction == 6)
    //        {
    //            inputDisplay.text += "RIGHT, ";
    //        }

    //        //up
    //        if (direction == 8)
    //        {
    //            inputDisplay.text += "UP, ";
    //        }
    //    } 
    //}

    public void ClearInputDisplay()
    {
        if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
        {
            inputDisplay.text = "";
            inputDisplay.color = Color.white;
        }
    }

    public static string ConvertCodeToString(uint code, Color? color = null)
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





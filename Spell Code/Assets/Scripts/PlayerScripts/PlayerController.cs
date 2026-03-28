using BestoNet.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;

//using UnityEditor.Experimental.GraphView;

//using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

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
    private class PlayerToast
    {
        public TextMeshPro textMesh;
        public float elapsed;
        public Color baseColor;
    }

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
    private int _pendingHitboxOwnerIndex = -1;
    public InputSnapshot input;
    //public InputSnapshot bufferInput;
    public string characterName = "R-Cade";
    [Header("Haptics")]
    [SerializeField] private bool enableHitRumble = true;
    [SerializeField, Range(0f, 1f)] private float hitRumbleLow = 0.2f;
    [SerializeField, Range(0f, 1f)] private float hitRumbleHigh = 0.6f;
    [SerializeField] private float hitRumbleDuration = 0.12f;
    private Coroutine hitRumbleRoutine;

    public static readonly Fixed FixedDeltaTime = Fixed.FromFloat(1f / 60f);

    private ushort lerpDelay = 0;
    [NonSerialized]
    public FixedVec2 position;

    public bool facingRight = true;
    public bool isGrounded = false;
    public bool onPlatform = false;
    public bool relativeInputs = false; //whether the player's directional inputs should be relative to their facing direction, e.g. pressing left while facing left would give a 6 instead of a 4

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
    public const int TerminalVelocity = -20;
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
    public Texture2D[] matchPalette = new Texture2D[4];
    public Texture2D secretEpicPalette;
    private bool secretEpicPaletteActive = false;
    public Texture2D secretNormalPalette;
    private bool secretNormalPaletteActive = false;
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

    [HideInInspector]
    public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;
    public uint stateSpecificArg = 0; //use only within a state, not between them

    public uint storedCode = 0; //the code that is stored up for release

    public uint storedCodeMaxDuration = 0; //NAME THIS
    public uint storedCodeDuration = 0; //how many more logic frames the stored code will last before auto-releasing

    public byte comboCounter = 0;
    public ushort comboResetTimer = 0;
    public byte hitstop = 0;
    public bool hitstopActive = false;
    public bool hitstunOverride = false;

    public ushort iframes = 0;
    public bool lightArmor = false;

    public List<SpellData> spellList = new List<SpellData>();
    public GameObject basicProjectileInstance;
    private int _pendingHitboxProjectileIndex = -1;

    //TMPro
    public TextMeshPro inputDisplay;
    public bool removeInputDisplay;
    public TextMeshPro playerNum;
    public List<Image> playerIndexImages = new List<Image>();
    public bool test;

    //[SerializeField]
    public Color colorSuccess;


    //Toast Variables
    //[SerializeField]
    private float toastLifetime = 1f;
    //[SerializeField]
    private float toastFadeDuration = 0.35f;
    //[SerializeField]
    private float toastBaseVerticalOffset = 90;
    //[SerializeField]
    private float toastStackSpacing = 8f;
    //[SerializeField]
    private float toastRiseDistance = 2f;
    //[SerializeField]
    private float toastFontSize = 72;

    private readonly List<PlayerToast> activeToasts = new();
    private Transform toastRoot;

    //Player Data (for data saving and balancing, different from the above Character Data)
    public int spellsFired = 0;
    public int basicsFired = 0;
    public int spellsHit = 0;
    public bool basicSpawnOverride = false; //this is to prevent the basic projectile from spawning during certain spells that override the basic attack, like Amon Slash. It should be set to true during the spell's animation and set back to false at the end of the spell's animation.
    public Fixed timer = Fixed.FromInt(0);
    //public bool timerRunning = false;
    public List<Fixed> times = new List<Fixed>();

    public int roundsWon;

    public bool chosenSpell = false;
    public bool chosenStartingSpell = false;
    public bool isSpawned;
    public string startingSpell;
    public bool startingSpellAdded = false;

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
        if (!GameManager.Instance.isOnlineMatchActive)
        {
            InitCharacter();
            ProjectileManager.Instance.InitializeAllProjectiles();
        }

    }

    private void Update()
    {
        UpdateToasts();
    }

    private void OnDisable()
    {
        ClearToasts();

        //stop playing all repeating sounds for this player
        SFX_Manager.Instance.StopRepeatingPlayerSounds(Array.IndexOf(GameManager.Instance.players, this));
        StopHitRumble();
    }

    private void OnDestroy()
    {
        ClearToasts();

        //stop playing all repeating sounds for this player
        if(SFX_Manager.Instance != null) SFX_Manager.Instance.StopRepeatingPlayerSounds(Array.IndexOf(GameManager.Instance.players, this));
        StopHitRumble();
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

        currentPlayerHealth = charData.playerHealth;
        runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
        slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
        jumpForce = Fixed.FromInt(charData.jumpForce);
        playerWidth = Fixed.FromInt(charData.playerWidth);
        playerHeight = Fixed.FromInt(charData.playerHeight);

        startingSpell = charData.startingInventory[0];

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
                playerIndexImages[0].enabled = true;
                playerIndexImages[0].color = new Color32(255, 62, 117, 255);
                break;
            case 1:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P2";
                pID = 2;
                playerNum.color = Color.cyan;
                playerIndexImages[1].enabled = true;
                playerIndexImages[1].color = new Color32(67, 122, 252, 255);
                break;
            case 2:
                InitializePalette(matchPalette[2]);
                //playerNum.text = "P3";
                pID = 3;
                playerNum.color = Color.yellow;
                playerIndexImages[2].enabled = true;
                playerIndexImages[2].color = new Color32(255, 207, 0, 255);
                break;
            case 3:
                InitializePalette(matchPalette[3]);
                //playerNum.text = "P4";
                pID = 4;
                playerNum.color = Color.green;
                playerIndexImages[3].enabled = true;
                playerIndexImages[3].color = new Color32(107, 255, 116, 255);
                break;
        }

        //DELETE THIS LATER, JUST TO LOCK STARTING SPELL TO PID
        if (pID == 1) { startingSpell = "AmonSlash"; }
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
        ClearToasts();
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
        iframes = 180; //you get 3 sec of invul on spawn
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

    public void AdjustBrightnessForIframes()
    {
        float targetBrightness = IsInvincible() ? 0.128f : 1.0f;
        MaterialPropertyBlock propertyBlock = new();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        if (propertyBlock.GetFloat("_Brightness") != targetBrightness)
        {
            propertyBlock.SetFloat("_Brightness", targetBrightness);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }
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
        ClearToasts();
        ClearSpellList();

        startingSpellAdded = false;
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
            //Debug.LogWarning($"[GetRawKeyboardInput] SOME KEY IS PRESSED!");
            // Log specific keys
            //Debug.LogWarning($"W={UnityEngine.Input.GetKey(KeyCode.W)}, " +
            //                $"A={UnityEngine.Input.GetKey(KeyCode.A)}, " +
            //                $"S={UnityEngine.Input.GetKey(KeyCode.S)}, " +
            //                $"D={UnityEngine.Input.GetKey(KeyCode.D)}");
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

        //Debug.Log($"[GetRawKeyboardInput] Direction={direction}, Code={codeState}, Jump={jumpState}");

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
            else if (vSpd > Fixed.FromInt(TerminalVelocity))
            {
                vSpd -= gravity / Fixed.FromInt(2); // Halve gravity if falling
            }

        }

        //check the comboResetTimer for combo breaker Purposes
        if(state != PlayerState.Hitstun && comboResetTimer > 0)
        {
            comboResetTimer--;
            if(comboResetTimer <= 0)
            {
                comboCounter = 0;
            }
        }

        //check for releasing a stored code
        CheckReleaseCode(input);



#region ---------------------------------PLAYER STATE MACHINE---------------------------------
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

                    //play the jump dust VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.JUMP_DUST, position, pID, facingRight);

                    SetState(PlayerState.Jump);
                    break;
                }
                LerpHspd(Fixed.FromInt(0), 3);
                break;
            case PlayerState.Run:
                //if the logic frame is a frame at which the player should make a run sound,...
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

                    //play the jump dust VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.JUMP_DUST, position, pID, facingRight);

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

                //this is an update for whether the jump animation should be rising or falling.
                AnimationManager.Instance.SetJumpAnimation(this);
                //Check Direction Inputs
                if (isGrounded)
                {
                    SetState(PlayerState.Idle);

                    //play the jump dust VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.JUMP_DUST, position, pID, facingRight);

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
                timer += FixedDeltaTime;

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
                        if(relativeInputs)
                        {
                            currentInput = facingRight ? (byte)0b10 : (byte)0b01;
                        }
                        else
                        {
                            currentInput = 0b10;
                        }
                        break;
                    case 6:
                        if (relativeInputs)
                        {
                            currentInput = facingRight ? (byte)0b01 : (byte)0b10;
                        }
                        else
                        {
                            currentInput = 0b01;
                        }
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
                    byte tempInput;
                    switch (input.Direction)
                    {
                        case 2:
                            // Set the 2 highest significant bits minus 2 bits per codeCount to 00
                            // stateSpecificArg: [high bits ...][codeCount (lowest 4 bits)]
                            // Example: For codeCount = 1, clear bits 31-30; for codeCount = 2, clear bits 29-28, etc.
                            stateSpecificArg |= (uint)(0b00 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("down input Pressed!");
                            //play the input down code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE_DOWN, 0.95f, 0.95f);
                            break;
                        case 4:
                            if(relativeInputs)
                            {
                                tempInput = facingRight ? (byte)0b10 : (byte)0b01;

                            }
                            else
                            {
                                tempInput = 0b10;
                            }
                                stateSpecificArg |= (uint)(tempInput << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("left input Pressed!");
                            //play the input left code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE_LEFT, 1.05f, 1.05f);
                            break;
                        case 6:
                            if (relativeInputs)
                            {
                                tempInput = facingRight ? (byte)0b01 : (byte)0b10;
                            }
                            else
                            {
                                tempInput = 0b01;
                            }
                            stateSpecificArg |= (uint)(tempInput << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("right input Pressed!");
                            //play the input right code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE_RIGHT, 1f, 1f);
                            break;
                        case 8:
                            stateSpecificArg |= (uint)(0b11 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            Debug.Log("up input Pressed!");
                            //play the input up code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE_UP, 1.1f, 1.1f);
                            break;
                        default:
                            //stateSpecificArg &= ~(1u << 4);
                            break;
                    }

                    // Increment the last 4 bits of stateSpecificArg by 1
                    if ((stateSpecificArg & (1u << 4)) == 0)
                    {
                        stateSpecificArg = (stateSpecificArg & ~0xFu) | (((stateSpecificArg & 0xFu) + 1) & 0xFu);
                        storedCodeDuration = 0;
                    }
                    //Debug.Log($"currentCode: {Convert.ToString(stateSpecificArg, toBase: 2)}");
                }

                inputDisplay.text = ConvertCodeToString(stateSpecificArg);

                //set the 5th bit to 0 to indicate we are no longer primed
                //uint checkedSpellInput = stateSpecificArg &~(1u << 4);
                //stateSpecificArg &= ~(1u << 4);

                //loop through spells to see if your current input matches any of your spells
                bool spellMatched = false;
                for (int i = i = 0; i < spellList.Count; i++)
                {
                    if (spellList[i].spellInput == (stateSpecificArg& ~(1u << 4)) &&
                        spellList[i].spellType == SpellType.Active &&
                        spellList[i].cooldownCounter <= 0)
                    {
                        spellMatched = true;
                        //increment the store code timer (charging up to store)
                        if(storedCodeDuration == 0) SpawnToast($"{spellList[i].spellName.ToUpper()}!", Color.white);
                        storedCodeDuration += 3;
                        if (input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                        {
                            lightArmor = false;
                            //set the 5th bit to 0 to indicate we are no longer primed
                            stateSpecificArg &= ~(1u << 4);

                            //reset the storedCode timer
                            storedCodeDuration = 0;
                            SetState(PlayerState.CodeRelease, stateSpecificArg);

                            break;
                        }

                        //jump button pressed or held for long enough
                        if (input.ButtonStates[1] == ButtonState.Pressed || storedCodeDuration >= 240)
                        {
                            lightArmor = false;
                            //set the 5th bit to 0 to indicate we are no longer primed
                            stateSpecificArg &= ~(1u << 4);
                            //if the current code is a valid spell code, store it for later use
                            
                            storedCode = stateSpecificArg;

                            uint spellCodeLength = (storedCode & 0xFu);
                            storedCodeDuration = Math.Clamp(6 - spellCodeLength, 0, 6) * 60; //stored code lasts for 6 seconds (360 logic frames) minus 1 second (60 logic frames) per input in the code
                            SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                            SpawnToast("STORED!", Color.white);
                            //break;
                            
                        }
                        break;
                    }
                }


                //handle the button cases for invalid inputs
                if (!spellMatched)
                {
                    //reset the storedCode timer
                        storedCodeDuration = 0;
                    //code button released
                    if (input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                    {
                        lightArmor = false;
                        
                        SetState(PlayerState.CodeRelease, stateSpecificArg);

                        break;
                    }
                    //jump button pressed
                    if (input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        ClearInputDisplay();
                        stateSpecificArg = 0;
                        SpawnToast("INPUTS CLEARED!", Color.white);
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
                    uint testCode = stateSpecificArg & ~(1u << 4);
                    if (testCode == 0b_0000_0000_0110_0110_0000_1111_0000_1000) //Konami Code input
                    {
                        Debug.Log("Konami Code Activated!");
                        if (!secretEpicPaletteActive)
                        {
                            SpawnToast("Hey Lois, I'm in Spell Code SlingerZ!", Color.white);
                            InitializePalette(secretEpicPalette);
                            secretEpicPaletteActive = true;
                        }
                        else
                        {
                            InitializePalette(matchPalette[pID - 1]);
                            secretEpicPaletteActive = false;
                        }
                    }
                    if (testCode == 0b_0000_0000_1001_1001_1111_0000_0000_1000) //Inverse Konami Code input
                    {
                        Debug.Log("Inverse Konami Code Activated!");
                        if (!secretNormalPaletteActive)
                        {
                            SpawnToast("I'm in Spell Code SlingerZ, Giggity!", Color.white);
                            InitializePalette(secretNormalPalette);
                            secretNormalPaletteActive = true;
                        }
                        else
                        {
                            InitializePalette(matchPalette[pID - 1]);
                            secretNormalPaletteActive = false;
                        }
                    }
                    if (testCode == 0b_0000_0000_0000_0000_0000_0000_0000_1100) //12 downs
                    {
                        Debug.Log("Relative Inputs activated!");
                        relativeInputs = !relativeInputs;
                        string activeWord = relativeInputs?"ACTIVATED":"DEACTIVATED";
                        SpawnToast($"RELATIVE INPUTS {activeWord}!", Color.white);
                    }
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
                            SpawnToast("ON COOLDOWN!",Color.white);
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

                    if (!basicSpawnOverride)
                    {
                        //create an instance of your basic spell here
                        BaseProjectile newProjectile = ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                        ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new FixedVec2(Fixed.FromInt(16), Fixed.FromInt(36)));
                        
                    }
                    else
                    {
                        basicSpawnOverride = false;
                    }

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
                    SetState(input.ButtonStates[0] == ButtonState.Held  && storedCodeDuration <=0? PlayerState.CodeWeave : PlayerState.Idle);
                    break;
                }

                if (logicFrame >= 2/*CharacterDataDictionary.GetTotalAnimationFrames(characterName, PlayerState.Tech)*/)
                {
                    if(input.ButtonStates[0] == ButtonState.Held  && storedCodeDuration <=0)
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

#endregion
        //Check conditions of all spells with the onupdate condition
        CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnUpdate);

        UpdateResources();

        //check player collisions
        PlayerWorldCollisionCheck();

        position += new FixedVec2(hSpd, vSpd);
        //position.x += hSpd;
        //position.y += vSpd;

        if (ShouldLogDesyncFrame())
        {
            Debug.Log($"[DESYNC] f={GameManager.Instance.frameNumber} pid={pID} st={state} dir={input.Direction} b0={input.ButtonStates[0]} b1={input.ButtonStates[1]} raw={rawInput} h={hSpd.RawValue} v={vSpd.RawValue} x={position.X.RawValue} y={position.Y.RawValue} g={isGrounded} plat={onPlatform} lerp={lerpDelay} lf={logicFrame}");
        }

        //handle player animation
        List<int> frameLengths = AnimationManager.Instance.GetFrameLengthsForCurrentState(this);
        animationFrame = GetCurrentFrameIndex(frameLengths, CharacterDataDictionary.GetAnimFrames(characterName, state).loopAnim);

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.tempSpellDisplays[playerIndex].UpdateCooldownDisplay(playerIndex);

        //if the hurtboxgroup at your current logic frame and state has width and height of 0, then make the sprite renderer brighter to indicate invulnerability frames
        AdjustBrightnessForIframes();



        //check if we are in gameplay scene and if not, reset health to max to avoid dying in non-gameplay scenes
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != "Gameplay")
        {
            currentPlayerHealth = charData.playerHealth;
        }
        logicFrame++;
    }

    private bool ShouldLogDesyncFrame()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.isOnlineMatchActive || !gm.logDesyncTrace)
        {
            return false;
        }

        RollbackManager rb = RollbackManager.Instance;
        if (rb != null && rb.isRollbackFrame)
        {
            return false;
        }

        int interval = gm.logDesyncEveryNFrames;
        if (interval < 1)
        {
            interval = 1;
        }

        return gm.frameNumber % interval == 0;
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
    public bool IsInvincible()
    {
        if(iframes > 0)
        {
            //if you are invincible based on iframes, always return true
            return true;
        }
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

                for (int i = 0; i < activatableSolidCount; i++)
                {
                    float centerX = stageDataSO.activatableSolidCenter[i].x;
                    float centerY = stageDataSO.activatableSolidCenter[i].y;
                    bool isOpen = GameManager.Instance.IsGateOpenAtPosition(centerX, centerY);
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
        #region--- Borders ---
        if (stageDataSO.borderMin != null && stageDataSO.borderMax != null)
        {
            //switch between the stageDataSO borderTypes to determine how to handle border collisions (borders that stop, borders that wrap around, borders that kill you, etc.)
            switch (stageDataSO.borderType)
            {
                case BorderType.Collision:
                    FixedVec2 tempPos = position;
                    Fixed borderMinX = Fixed.FromFloat(stageDataSO.borderMin.x);
                    Fixed borderMaxX = Fixed.FromFloat(stageDataSO.borderMax.x);
                    Fixed borderMinY = Fixed.FromFloat(stageDataSO.borderMin.y);
                    Fixed borderMaxY = Fixed.FromFloat(stageDataSO.borderMax.y);
                    position = new FixedVec2(
                        Fixed.Clamp(position.X, borderMinX, borderMaxX),
                        Fixed.Clamp(position.Y, borderMinY, borderMaxY)
                    );

                    returnVal = !tempPos.Equals(position);
                    break;
                case BorderType.Loop:
                    if (position.X.ToFloat() > stageDataSO.borderMax.x)
                    {
                        position = FixedVec2.FromFloat(stageDataSO.borderMin.x, position.Y.ToFloat());
                        returnVal = true;
                    }
                    else if (position.X.ToFloat() < stageDataSO.borderMin.x)
                    {
                        position = FixedVec2.FromFloat(stageDataSO.borderMax.x, position.Y.ToFloat());
                        returnVal = true;
                    }

                    if (position.Y.ToFloat() > stageDataSO.borderMax.y)
                    {
                        position = FixedVec2.FromFloat(position.X.ToFloat(), stageDataSO.borderMin.y);
                        returnVal = true;
                    }
                    else if (position.Y.ToFloat() < stageDataSO.borderMin.y)
                    {
                        position = FixedVec2.FromFloat(position.X.ToFloat(), stageDataSO.borderMax.y);
                        returnVal = true;
                    }
                    break;
                case BorderType.DeathZone:
                    if (position.X.ToFloat() > stageDataSO.borderMax.x || position.X.ToFloat() < stageDataSO.borderMin.x ||
                        position.Y.ToFloat() > stageDataSO.borderMax.y || position.Y.ToFloat() < stageDataSO.borderMin.y)
                    {
                        //kill player and respawn at spawn point
                        currentPlayerHealth = 0;
                        returnVal = true;
                    }
                    break;
            }
        }
        #endregion
        return returnVal;
    }

    public void SetState(PlayerState targetState, uint inputSpellArg = 0)
    {


        prevState = state;
        HandleExitState(prevState);
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
                if(storedCodeDuration <= 0)//this check is because of the test of allowing store to persist
                {
                    ClearInputDisplay();
                }


                lightArmor = false;

                //reset storedCode if you get hit
                // storedCode = 0;
                // storedCodeDuration = 0;

                stateSpecificArg = hitboxData.hitstun;
                Fixed xKnockback = Fixed.FromInt(hitboxData.xKnockback);
                Fixed yKnockback = Fixed.FromInt(hitboxData.yKnockback);
                if (GameManager.Instance.isOnlineMatchActive)
                {
                    hSpd = xKnockback;
                    vSpd = yKnockback;
                    facingRight = hitboxData.xKnockback < 0;
                }
                else
                {
                    hSpd = xKnockback * (facingRight ? Fixed.FromInt(-1) : Fixed.FromInt(1));
                    vSpd = yKnockback;
                }

                //if (isGrounded)
                //{
                //    position.y = StageData.Instance.floorYval + 1;
                //    isGrounded = false;
                //}

                break;

            case PlayerState.Tech:
                //hSpd = facingRight ? Fixed.FromInt(-1) : Fixed.FromInt(1);
                //vSpd = Fixed.FromInt(5);

                comboResetTimer = 45;
                break;
            case PlayerState.CodeWeave:
                lightArmor = true;
                //play codeweave sound
                SFX_Manager.Instance.PlaySound(Sounds.ENTER_CODE_WEAVE);

                //begin to continuously play the code weave sound
                SFX_Manager.Instance.StartRepeatingSound(Sounds.CONTINUOUS_CODE_WEAVE, 0.42f, Array.IndexOf(GameManager.Instance.players, this), 0.8f, 1.2f);

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
                playerHeight = Fixed.FromInt(charData.playerHeight/2);
                break;
        }
    }
    //exit logic:
    private void HandleExitState(PlayerState prevStateparam)
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
                hitstunOverride = false;
                ClearInputDisplay();
                break;
            case PlayerState.Slide:
                playerHeight = Fixed.FromInt(charData.playerHeight);
                break;
        }
        stateSpecificArg = 0;
    }


    /// <summary>
    /// Updates spell resource values each frame
    /// </summary>
    public void UpdateResources()
    {
        //update flow state
        if (flowState > 0)
        {
            //play the flow state aura visual effect 
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.FLOW_STATE_AURA, position, pID, true, this.gameObject.transform, ((float)flowState / (float)maxFlowState) * 100f);

            flowState--;
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.FLOW_STATE_AURA, pID);
        }

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

            //Debug.Log("VFX Debugging | Player " + pID + "'s Demon Aura at " + (float)demonAura + ". And maxdemonAura at " + (float)maxDemonAura + ". And particle count at " + (((float)demonAura / (float)maxDemonAura) * 50f));

            //play the demon aura visual effect 
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMON_AURA, position, pID, true, this.gameObject.transform, (((float)demonAura / (float)maxDemonAura) * 50f));
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.DEMON_AURA, pID);
        }

        if (stockStability > 0)
        {
            //play the stock aura visual effect 
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.STOCK_AURA, position, pID, true, this.gameObject.transform, Mathf.Clamp(((float)stockStability / 100f), 0f, 1f) * 100f);
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.STOCK_AURA, pID);
        }

        if (reps > 0)
        {
            //play the reps visual effect 
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.REPS_AURA, position + FixedVec2.FromFloat(0f, 42f), pID, true, this.gameObject.transform, (float)reps * 20f);
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.REPS_AURA, pID);
        }
    }

    /// <summary>
    /// this function makes the player take damage outside of hitstun, notably from spell effect damage
    /// </summary>
    /// <param name="damageAmount"></param>
    public void TakeEffectDamage(int damageAmount, PlayerController attacker)
    {
        if (GameManager.Instance.currentStageIndex < 0)
        {
            //don't take damage in the lobby
            return;
        }

        HandleDamage(attacker, damageAmount);
    }

    public void CheckHit(InputSnapshot input)
    {
        if(iframes > 0)
        {
            iframes--;
            return;
        }
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

            //ignore hit if we are in codeweave and the attack level is less than 2 (basic attack)
            if (lightArmor && hitboxData.attackLvl < 2)
            {
                SpawnToast($"BLOCKED!", Color.white);
                return;
            }

            //mySFXHandler.PlaySound(SoundType.DAMAGED);

            if (GameManager.Instance.currentStageIndex < 0)
            {
                //don't take damage in the lobby
                return;
            }

            

            HandleDamage(attacker, hitboxData.damage);
            
            comboCounter++;
            if (comboCounter >= 4)
            {
                SpawnToast("COMBO BREAK!!!", Color.magenta);
                iframes = 120;
                comboCounter = 0;

                //Play the combo break VFX
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.COMBO_BREAKER, position + FixedVec2.FromFloat(0f, -38f), pID);
            }


            //GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));

            //play the damaged sound
            SFX_Manager.Instance.PlaySound(Sounds.HIT);

            //play the damage VFX
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DAMAGE, position + FixedVec2.FromFloat(0f, 42f), pID, facingRight);

            SetState(PlayerState.Hitstun);

            //call the active on hit proc of the spell that created the projectile that hit us
            if (hitboxData.parentProjectile.ownerSpell != null)
            {
                hitboxData.parentProjectile.ownerSpell.CheckCondition(this, ProcCondition.ActiveOnHit);
            }


            //call the checkProcEffect call of every spell that has ProcEffect.OnHit in the attacker's spell list
            if (attacker != null)
            {
                CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHit);
            }
            

            //now call the checkProcEffect call of every spell that has ProcEffect.OnHurt in this player's spell list
            CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurt);

            //now check for OnHitBasic or OnHitSpell depending on whether the hitbox was a basic attack hitbox
            if (hitboxData.basicAttackHitbox)
            {
                if (attacker != null)
                {
                    CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHitBasic);
                }
                CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurtBasic);
            }
            else
            {
                if (attacker != null)
                {
                    CheckAllSpellConditionsOfProcCon(attacker, ProcCondition.OnHitSpell);
                }
                CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnHurtSpell);

                if (attacker != null && attacker.demonAura > 0)
                {
                    attacker.demonAuraLifeSpanTimer = 360; //refresh demon aura lifespan timer on spell hit to 6 seconds (360 frames)
                }
            }

            //subtract demon aura based on the hitbox's damage
            //demonAura = (ushort)Math.Max(0, demonAura - (int)hitboxData.damage);

            if (GameManager.Instance.isOnlineMatchActive)
            {
                isHit = false;
                hitboxData = null;
            }


        }
    }
    private void HandleDamage(PlayerController attacker, int damageAmount)
    {
        bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;
        bool hasAttacker = attacker != null;
        if (!isRollback && damageAmount > 0)
        {
            TriggerHitRumble(0.2f, 0.6f, 0.12f);
        }

        if (DataManager.Instance != null &&
            DataManager.Instance.gameData != null &&
            DataManager.Instance.gameData.arenaData != null)
        {
            var arenaData = DataManager.Instance.gameData.arenaData;
            if (!arenaData.hitDict.TryGetValue(GameManager.Instance.currentStage, out List<Vector2> hitList))
            {
                hitList = new List<Vector2>();
                arenaData.hitDict[GameManager.Instance.currentStage] = hitList;
            }
            hitList.Add(transform.position);

            //update the damage matrix the attacker attacking this player
            if (!isRollback && hasAttacker)
            {
                GameManager.Instance.damageMatrix[pID - 1, attacker.pID - 1] += (byte)Math.Clamp(damageAmount, 0, currentPlayerHealth);
            }
        }

        //checking for death
        if (damageAmount >= currentPlayerHealth)
        {
            if (DataManager.Instance != null &&
                DataManager.Instance.gameData != null &&
                DataManager.Instance.gameData.arenaData != null)
            {
                var arenaData = DataManager.Instance.gameData.arenaData;
                if (!arenaData.deathDict.TryGetValue(GameManager.Instance.currentStage, out List<Vector2> deathList))
                {
                    deathList = new List<Vector2>();
                    arenaData.deathDict[GameManager.Instance.currentStage] = deathList;
                }
                ClearInputDisplay();
                deathList.Add(transform.position);
            }

            // play the controller vibration
            TriggerHitRumble(1f, 1f, 1f);

            //play the death sound
            SFX_Manager.Instance.PlaySound(Sounds.DEATH);

            //play the death visual effect
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEATH, position, pID);

            CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnDeath);

            currentPlayerHealth = 0;

            //award the killer with the extra bonus ram
            if (hasAttacker)
            {
                attacker.roundRam += baseRamKillBonus;
                attacker.totalRam += baseRamKillBonus;
                attacker.SpawnToast($"+{baseRamKillBonus} RAM", Color.yellow);
            }

        }
        else
        {
            // Reduce health 
            currentPlayerHealth = (ushort)(currentPlayerHealth - (int)damageAmount);
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

    public void ResolveReferences()
    {
        if (hitboxData != null && _pendingHitboxOwnerIndex >= 0)
        {
            // Use specific projectile index if available
            if (_pendingHitboxProjectileIndex >= 0 &&
                _pendingHitboxProjectileIndex < ProjectileManager.Instance.projectilePrefabs.Count)
            {
                hitboxData.parentProjectile = ProjectileManager.Instance.projectilePrefabs[_pendingHitboxProjectileIndex];
            }
            else if (_pendingHitboxOwnerIndex < GameManager.Instance.players.Length)
            {
                PlayerController ownerPlayer = GameManager.Instance.players[_pendingHitboxOwnerIndex];
                if (ownerPlayer != null)
                {
                    foreach (BaseProjectile proj in ProjectileManager.Instance.activeProjectiles)
                    {
                        if (proj.owner == ownerPlayer)
                        {
                            hitboxData.parentProjectile = proj;
                            break;
                        }
                    }
                }
            }
            _pendingHitboxOwnerIndex = -1;
            _pendingHitboxProjectileIndex = -1;
        }
    }

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
        bw.Write(playerWidth.RawValue);
        bw.Write(playerHeight.RawValue);
        bw.Write(runSpeed.RawValue);
        bw.Write(slideSpeed.RawValue);
        bw.Write(jumpForce.RawValue);
        bw.Write(gravity.RawValue);
        bw.Write(timer.RawValue);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write(onPlatform);
        bw.Write(relativeInputs);
        bw.Write((byte)state);
        bw.Write((byte)prevState);
        bw.Write(logicFrame); // 🔹 Save current logic frame
        bw.Write(animationFrame);
        bw.Write(lerpDelay);
        bw.Write(stateSpecificArg);
        bw.Write(hitstop);
        bw.Write(hitstopActive);
        bw.Write(hitstunOverride);
        bw.Write(comboCounter);
        bw.Write(comboResetTimer);
        bw.Write(lightArmor);
        bw.Write(basicSpawnOverride);
        bw.Write(storedCode);
        bw.Write(storedCodeDuration);
        bw.Write(currentPlayerHealth);
        bw.Write(isAlive);
        bw.Write(isHit);
        bw.Write(iframes);
        bw.Write(unchecked((int)0xAABBCCDD));

        bool hasHitboxData = hitboxData != null;
        bw.Write(hasHitboxData);
        if (hasHitboxData)
        {
            bw.Write(hitboxData.damage);
            bw.Write(hitboxData.hitstun);
            bw.Write(hitboxData.xKnockback);
            bw.Write(hitboxData.yKnockback);
            bw.Write(hitboxData.attackLvl);
            bw.Write(hitboxData.basicAttackHitbox);
            int ownerIndex = hitboxData.parentProjectile?.owner != null
                ? Array.IndexOf(GameManager.Instance.players, hitboxData.parentProjectile.owner)
                : -1;
            bw.Write(ownerIndex);
            int projPrefabIndex = hitboxData.parentProjectile != null
                ? ProjectileManager.Instance.projectilePrefabs.IndexOf(hitboxData.parentProjectile)
                : -1;
            bw.Write(projPrefabIndex);
        }
        bw.Write(unchecked((int)0xAABBCCDD));

        bw.Write(flowState);
        bw.Write(stockStability);
        bw.Write(demonAura);
        bw.Write(demonAuraLifeSpanTimer);
        bw.Write(reps);
        //bw.Write(momentum);
        //bw.Write(slimed);
        bw.Write(isSpawned);
        bw.Write(roundsWon);
        bw.Write(totalRam);
        bw.Write(roundRam);
        bw.Write(ramBounty);
        bw.Write(chosenStartingSpell);
        bw.Write(startingSpellAdded);
        bw.Write(unchecked((int)0xAABBCCDD));


        // Spell List Serialization
        bw.Write(unchecked((int)0xAABBCCDD));
        bw.Write(spellList.Count);
        for (int i = 0; i < spellList.Count; i++)
        {
            bw.Write(spellList[i].spellName);

            using (MemoryStream tempStream = new MemoryStream())
            using (BinaryWriter tempWriter = new BinaryWriter(tempStream))
            {
                spellList[i].Serialize(tempWriter);
                byte[] spellBytes = tempStream.ToArray();
                bw.Write(spellBytes.Length);
                bw.Write(spellBytes);
            }
        }

        //bw.Write(InputConverter.ConvertFromInputSnapshot(bufferInput));
    }


    public void Deserialize(BinaryReader br)
    {
        position = new FixedVec2(new Fixed(br.ReadInt32()), new Fixed(br.ReadInt32())); // Assuming Fixed32 uses int
        hSpd = new Fixed(br.ReadInt32());
        vSpd = new Fixed(br.ReadInt32());
        playerWidth = new Fixed(br.ReadInt32());
        playerHeight = new Fixed(br.ReadInt32());
        runSpeed = new Fixed(br.ReadInt32());
        slideSpeed = new Fixed(br.ReadInt32());
        jumpForce = new Fixed(br.ReadInt32());
        gravity = new Fixed(br.ReadInt32());
        timer = new Fixed(br.ReadInt32());
        facingRight = br.ReadBoolean();
        isGrounded = br.ReadBoolean();
        onPlatform = br.ReadBoolean();
        relativeInputs = br.ReadBoolean();
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
        comboCounter = br.ReadByte();
        comboResetTimer = br.ReadUInt16();
        lightArmor = br.ReadBoolean();
        basicSpawnOverride = br.ReadBoolean();
        storedCode = br.ReadUInt32();
        storedCodeDuration = br.ReadUInt32();
        currentPlayerHealth = br.ReadUInt16();
        isAlive = br.ReadBoolean();
        isHit = br.ReadBoolean();
        iframes = br.ReadUInt16();
        int markerA = br.ReadInt32();
        if (markerA != unchecked((int)0xAABBCCDD)) Debug.LogError($"MISALIGN at A: {markerA:X8}");

        bool hasHitboxData = br.ReadBoolean();
        if (hasHitboxData)
        {
            if (hitboxData == null) hitboxData = new HitboxData();
            hitboxData.damage = br.ReadUInt16();
            hitboxData.hitstun = br.ReadUInt16();
            hitboxData.xKnockback = br.ReadInt32();
            hitboxData.yKnockback = br.ReadInt32();
            hitboxData.attackLvl = br.ReadByte();
            hitboxData.basicAttackHitbox = br.ReadBoolean();
            _pendingHitboxOwnerIndex = br.ReadInt32();
            _pendingHitboxProjectileIndex = br.ReadInt32();
        }
        else
        {
            hitboxData = null;
            _pendingHitboxOwnerIndex = -1;
        }

        int markerB = br.ReadInt32();
        if (markerB != unchecked((int)0xAABBCCDD)) Debug.LogError($"MISALIGN at B: {markerB:X8}");
        flowState = br.ReadUInt16();
        stockStability = br.ReadUInt16();
        demonAura = br.ReadUInt16();
        demonAuraLifeSpanTimer = br.ReadUInt16();
        reps = br.ReadUInt16();
        //momentum = br.ReadUInt16();
        //slimed = br.ReadBoolean();
        isSpawned = br.ReadBoolean();
        roundsWon = br.ReadInt32();
        totalRam = br.ReadUInt16();
        roundRam = br.ReadUInt16();
        ramBounty = br.ReadInt16();
        chosenStartingSpell = br.ReadBoolean();
        bool savedStartingSpellAdded = br.ReadBoolean();
        int markerC = br.ReadInt32();
        if (markerC != unchecked((int)0xAABBCCDD)) Debug.LogError($"MISALIGN at C: {markerC:X8}");
        //bufferInput = InputConverter.ConvertFromShort(br.ReadInt16());

        // Spell List Deserialization
        int markerD = br.ReadInt32();
        if (markerD != unchecked((int)0xAABBCCDD)) Debug.LogError($"MISALIGN at D: {markerD:X8}");

        int spellCount = br.ReadInt32();

        // HANDLE SPELL LIST SYNC BASED ON FLAG
        if (savedStartingSpellAdded && !startingSpellAdded)
        {
            // Saved state had the spell, but we don't - need to add it
            if (!string.IsNullOrEmpty(startingSpell))
            {
                //Debug.Log($"[ROLLBACK] Re-adding starting spell: {startingSpell}");
                AddSpellToSpellList(startingSpell);
                startingSpellAdded = true;
            }
        }
        else if (!savedStartingSpellAdded && startingSpellAdded)
        {
            // Saved state didn't have the spell, but we do - need to remove it
            if (!string.IsNullOrEmpty(startingSpell))
            {
                //Debug.Log($"[ROLLBACK] Removing starting spell: {startingSpell}");
                RemoveSpellFromSpellList(startingSpell);
                startingSpellAdded = false;
            }
        }

        startingSpellAdded = savedStartingSpellAdded;

        // Read serialized spell payloads first
        List<(string name, byte[] data)> savedSpells = new List<(string name, byte[] data)>(spellCount);
        for (int i = 0; i < spellCount; i++)
        {
            string spellName = br.ReadString();
            int spellDataLength = br.ReadInt32();
            byte[] spellBytes = br.ReadBytes(spellDataLength);
            savedSpells.Add((spellName, spellBytes));
        }

        if (spellList.Count != spellCount)
        {
            Debug.LogError($"Spell list size mismatch during Deserialize! Expected {spellCount}, got {spellList.Count}. Rebuilding list from saved names.");
            RebuildSpellListFromSaved(savedSpells);
        }

        // Deserialize spell state into matching instances
        for (int i = 0; i < savedSpells.Count; i++)
        {
            string spellName = savedSpells[i].name;
            byte[] spellBytes = savedSpells[i].data;
            SpellData spellInstance = spellList.FirstOrDefault(s => s.spellName == spellName);
            if (spellInstance != null)
            {
                using (MemoryStream tempStream = new MemoryStream(spellBytes))
                using (BinaryReader tempReader = new BinaryReader(tempStream))
                {
                    spellInstance.Deserialize(tempReader);
                }
            }
            else
            {
                Debug.LogWarning($"Spell '{spellName}' not found after rebuild - skipped {spellBytes.Length} bytes");
            }
        }
    }

    private void TriggerHitRumble(float low, float high, float duration)
    {
        if (!enableHitRumble)
        {
            return;
        }

        Gamepad gamepad = GetAssignedGamepad();
        if (gamepad == null)
        {
            return;
        }

        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);
        duration = Mathf.Max(0f, duration);

        if (hitRumbleRoutine != null)
        {
            StopCoroutine(hitRumbleRoutine);
        }

        hitRumbleRoutine = StartCoroutine(HitRumbleRoutine(gamepad, low, high, duration));
    }

    private IEnumerator HitRumbleRoutine(Gamepad gamepad, float low, float high, float duration)
    {
        gamepad.SetMotorSpeeds(low, high);
        yield return new WaitForSeconds(duration);
        gamepad.SetMotorSpeeds(0f, 0f);
        hitRumbleRoutine = null;
    }

    private void StopHitRumble()
    {
        if (hitRumbleRoutine != null)
        {
            StopCoroutine(hitRumbleRoutine);
            hitRumbleRoutine = null;
        }

        Gamepad gamepad = GetAssignedGamepad();
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    private Gamepad GetAssignedGamepad()
    {
        if (TryGetComponent<PlayerInput>(out PlayerInput playerInput))
        {
            for (int i = 0; i < playerInput.devices.Count; i++)
            {
                if (playerInput.devices[i] is Gamepad gp)
                {
                    return gp;
                }
            }
        }

        if (playerInputs != null && playerInputs.devices.HasValue)
        {
            var devices = playerInputs.devices.Value;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i] is Gamepad gp)
                {
                    return gp;
                }
            }
        }

        return null;
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

    private void RebuildSpellListFromSaved(List<(string name, byte[] data)> savedSpells)
    {
        // Destroy existing spell instances
        for (int i = spellList.Count - 1; i >= 0; i--)
        {
            SpellData spell = spellList[i];
            if (spell != null)
            {
                Destroy(spell.gameObject);
            }
        }
        spellList.Clear();

        // Recreate list in saved order (no LoadSpell to avoid side effects)
        for (int i = 0; i < savedSpells.Count; i++)
        {
            string spellName = savedSpells[i].name;
            if (SpellDictionary.Instance != null &&
                SpellDictionary.Instance.spellDict != null &&
                SpellDictionary.Instance.spellDict.TryGetValue(spellName, out SpellData template) &&
                template != null)
            {
                SpellData instance = Instantiate(template);
                instance.owner = this;
                spellList.Add(instance);
            }
            else
            {
                Debug.LogWarning($"RebuildSpellListFromSaved: Missing spell '{spellName}' in dictionary.");
            }
        }

        // Recompute brand flags from rebuilt list
        vWave = false;
        killeez = false;
        DemonX = false;
        bigStox = false;
        for (int i = 0; i < spellList.Count; i++)
        {
            SpellData spell = spellList[i];
            if (spell == null || spell.brands == null) continue;
            for (int b = 0; b < spell.brands.Length; b++)
            {
                if (spell.brands[b] == Brand.VWave) vWave = true;
                if (spell.brands[b] == Brand.Killeez) killeez = true;
                if (spell.brands[b] == Brand.DemonX) DemonX = true;
                if (spell.brands[b] == Brand.BigStox) bigStox = true;
            }
        }

        // Rebuild projectile pool once to match the new spell list
        if (ProjectileManager.Instance != null)
        {
            ProjectileManager.Instance.InitializeAllProjectiles();
        }

        // Update UI if available
        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        if (playerIndex >= 0 && GameManager.Instance.tempSpellDisplays != null &&
            playerIndex < GameManager.Instance.tempSpellDisplays.Length &&
            GameManager.Instance.tempSpellDisplays[playerIndex] != null)
        {
            GameManager.Instance.tempSpellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
        }
    }


    public void CheckReleaseCode(InputSnapshot targetInput)
    {
        if (storedCode == 0)
        {
            return;
        }

        if (storedCodeDuration > 0 && state != PlayerState.CodeWeave)
        {
            storedCodeDuration--;
        }

        if (targetInput.ButtonStates[0] == ButtonState.Released || storedCodeDuration <= 0)
        {

            if (IsStorableState())
            {
                //this is to keep the physics interactions between releasing a stored code and a normal code consistent, improving player experience
                if(vSpd < Fixed.FromInt(0))
                {
                    vSpd = Fixed.FromInt(0);
                }
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

    public void ClearInputDisplay()
    {
        if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
        {
            inputDisplay.text = "";
            inputDisplay.color = Color.white;
        }
    }

    public void SpawnToast(string text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame)
        {
            return;
        }

        EnsureToastRoot();

        GameObject toastObject = new($"{name}_Toast");
        toastObject.transform.SetParent(toastRoot, false);

        TextMeshPro toastText = toastObject.AddComponent<TextMeshPro>();
        toastText.text = text;
        toastText.color = color;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.fontSize = toastFontSize;
        toastText.fontStyle = FontStyles.Bold;
        toastText.textWrappingMode = TextWrappingModes.NoWrap;
        toastText.overflowMode = TextOverflowModes.Overflow;
        toastText.raycastTarget = false;
        toastText.sortingOrder = 100;

        Renderer toastRenderer = toastText.GetComponent<Renderer>();
        if (toastRenderer != null)
        {
            toastRenderer.sortingLayerID = GetFrontmostSortingLayerId();
            toastRenderer.sortingOrder = short.MaxValue;
        }

        activeToasts.Add(new PlayerToast
        {
            textMesh = toastText,
            elapsed = 0f,
            baseColor = color
        });

        UpdateToastVisuals();
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

    private void EnsureToastRoot()
    {
        if (toastRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find("ToastRoot");
        if (existingRoot != null)
        {
            toastRoot = existingRoot;
        }
        else
        {
            GameObject toastRootObject = new("ToastRoot");
            toastRoot = toastRootObject.transform;
            toastRoot.SetParent(transform, false);
        }

        toastRoot.localPosition = new Vector3(0f, 0f, -0.1f);
        toastRoot.localRotation = Quaternion.identity;
        toastRoot.localScale = Vector3.one;
    }

    private void UpdateToasts()
    {
        if (activeToasts.Count == 0)
        {
            return;
        }

        float lifetime = Mathf.Max(0.01f, toastLifetime);
        for (int i = activeToasts.Count - 1; i >= 0; i--)
        {
            PlayerToast toast = activeToasts[i];
            if (toast == null || toast.textMesh == null)
            {
                activeToasts.RemoveAt(i);
                continue;
            }

            toast.elapsed += Time.deltaTime;
            if (toast.elapsed >= lifetime)
            {
                Destroy(toast.textMesh.gameObject);
                activeToasts.RemoveAt(i);
            }
        }

        if (activeToasts.Count == 0)
        {
            return;
        }

        UpdateToastVisuals();
    }

    private void UpdateToastVisuals()
    {
        float lifetime = Mathf.Max(0.01f, toastLifetime);
        float fadeDuration = Mathf.Clamp(toastFadeDuration, 0f, lifetime);
        float fadeStart = lifetime - fadeDuration;

        for (int i = 0; i < activeToasts.Count; i++)
        {
            PlayerToast toast = activeToasts[i];
            if (toast == null || toast.textMesh == null)
            {
                continue;
            }

            float normalizedLifetime = Mathf.Clamp01(toast.elapsed / lifetime);
            float alpha = toast.baseColor.a;
            if (fadeDuration > 0f && toast.elapsed > fadeStart)
            {
                float fadeProgress = Mathf.InverseLerp(fadeStart, lifetime, toast.elapsed);
                alpha *= 1f - fadeProgress;
            }

            Color displayColor = toast.baseColor;
            displayColor.a = alpha;
            toast.textMesh.color = displayColor;

            int stackIndex = (activeToasts.Count - 1) - i;
            float yOffset = toastBaseVerticalOffset + (stackIndex * toastStackSpacing) + (normalizedLifetime * toastRiseDistance);
            toast.textMesh.transform.localPosition = new Vector3(0f, yOffset, 0f);
        }
    }

    private void ClearToasts()
    {
        for (int i = activeToasts.Count - 1; i >= 0; i--)
        {
            PlayerToast toast = activeToasts[i];
            if (toast != null && toast.textMesh != null)
            {
                Destroy(toast.textMesh.gameObject);
            }
        }

        activeToasts.Clear();
    }

    private static int GetFrontmostSortingLayerId()
    {
        SortingLayer[] sortingLayers = SortingLayer.layers;
        if (sortingLayers == null || sortingLayers.Length == 0)
        {
            return 0;
        }

        SortingLayer frontmostLayer = sortingLayers[0];
        for (int i = 1; i < sortingLayers.Length; i++)
        {
            if (sortingLayers[i].value > frontmostLayer.value)
            {
                frontmostLayer = sortingLayers[i];
            }
        }

        return frontmostLayer.id;
    }
}

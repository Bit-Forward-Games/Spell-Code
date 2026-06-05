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
using UnityEngine.Rendering;
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

    private class PlayerDamageNumber
    {
        public TextMeshPro textMesh;
        public float elapsed;
        public Color baseColor;
        public Vector3 startOffset;
        public Vector3 drift;
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
    private InputAction pauseAction;
    private readonly bool[] direction = new bool[4];
    private readonly bool[] codeButton = new bool[2];
    private readonly bool[] jumpButton = new bool[2];
    private readonly bool[] pauseButton = new bool[2];
    private readonly ButtonState[] buttons = new ButtonState[3];
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
    public bool toggleCodeInput = false;
    public bool tapJump = false;
    private bool tapJumpPrimed = true;

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
    public byte jumpCount = 0;
    public byte maxJumpCount = 0;

    //Spell Resource Variables
    public ushort flowState = 0; //the timer for how long you are in flow state
    public ushort stockStability = 0; //percentage chance to crit before modifiers, e.g. 25 = 25% chance
    public ushort stockStabilityModified = 0; //crit chance after modifiers
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
    public const ushort baseRamKillBonus = 50;
    [NonSerialized]
    public const ushort baseRamLifeWorth = 250;

    // Push Box Variables
    [HideInInspector]
    public Fixed playerWidth;
    [HideInInspector]
    public Fixed playerHeight;

    [NonSerialized]
    public HitboxData hitboxData = null; //this represents what they are hit by
    public bool isHit = false;

    // Monotonically incremented each time HitboxManager registers a hit on this player.
    // Used by the UI damage bar to fire its animation exactly once per hit, even when
    // online rollback resim re-runs HitboxManager and re-sets isHit. 
    public uint damageBarHitCount = 0;

    public uint stateSpecificArg = 0; //use only within a state, not between them

    public uint storedCode = 0; //the code that is stored up for release

    public uint storedCodeMaxDuration = 0; //NAME THIS
    public uint storedCodeDuration = 0; //how many more logic frames the stored code will last before auto-releasing

    public byte comboCounter = 0;
    public ushort comboResetTimer = 0;
    public byte hitstop = 0;
    public bool hitstopActive = false;
    public bool superArmor = false;

    public ushort iframes = 0;
    public bool armor = false;

    [NonSerialized]
    public List<SpellData> spellList = new List<SpellData>();

    // Per-player pool of spell instances keyed by serialization id
    // RebuildSpellListFromSaved can REUSE instances instead of Destroy()/Instantiate() on every
    // rollback resim (the GameObject create/destroy churn that's catastrophic during lobby rollback
    // storms). Reuse is determinism-safe: a spell's template constants are set in its C# constructor
    // (run once, never change) and its sim-relevant runtime state is restored by Deserialize right
    // after the rebuild, while projectileInstances is rebuilt by InitializeAllProjectiles. A stack
    // per id supports duplicate copies of the same spell. Pure runtime cache -- not serialized and
    // not hashed, so its (possibly client-specific) contents never affect determinism.
    [NonSerialized]
    private readonly Dictionary<int, Stack<SpellData>> spellInstancePool = new Dictionary<int, Stack<SpellData>>();
    [NonSerialized]
    public List<SpellData> sortedSpellList = new List<SpellData>(); // reused buffer; refilled in place by BuildSortedSpellList (no per-call allocation)
    public List<SpellData> universalSpells = new List<SpellData>();
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
    private float toastLifetime = 1.2f;
    //[SerializeField]
    private float toastFadeDuration = 0.25f;
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

    //Damage Number Variables
    private float damageNumberLifetime = 0.85f;
    private float damageNumberFadeDuration = 0.25f;
    private float damageNumberBaseVerticalOffset = 52f;
    private float damageNumberRiseDistance = 34f;
    private float damageNumberHorizontalDrift = 24f;
    private float damageNumberGravityFallDistance = 28f;
    private float damageNumberFontSize = 84f;

    private readonly List<PlayerDamageNumber> activeDamageNumbers = new();
    private Transform damageNumberRoot;

    //Player Data (for data saving and balancing, different from the above Character Data)
    public int spellsFired = 0;
    public int basicsFired = 0;
    public int spellsHit = 0;
    public string basicSpawnOverride = ""; //this is to prevent the basic projectile from spawning during certain spells that override the basic attack, like Amon Slash. It should be set to true during the spell's animation and set back to false at the end of the spell's animation.
    public Fixed timer = Fixed.FromInt(0);
    //public bool timerRunning = false;
    public List<Fixed> times = new List<Fixed>();

    public int roundsWon;

    public bool chosenSpell = false;
    public bool chosenStartingSpell = false;
    public bool isSpawned;
    public string startingSpell;
    public bool startingSpellAdded = false;
    public bool suppressSpellLoadSideEffects = false;
    [NonSerialized]
    public int pID = -1;

    public bool npcOverride = false;

    //these variables are to track what collectives the player has. Passives for each collective
    //will only show up if the boolean is true
    public bool vWave = false;
    public bool killeez = false;
    public bool DemonX = false;
    public bool bigStox = false;

    public int _playerPauseIndex;


    private void Awake()
    {
        if (GetComponent<PlayerInput>().user.valid)
        {
            DontDestroyOnLoad(this.gameObject);
        }

        EnsureUniversalSpells();
    }
    void Start()
    {
        if(GetComponent<PlayerInput>() != null && GetComponent<PlayerInput>().user.valid)
        {
            upAction = playerInputs.actionMaps[0].FindAction("Up");
            downAction = playerInputs.actionMaps[0].FindAction("Down");
            leftAction = playerInputs.actionMaps[0].FindAction("Left");
            rightAction = playerInputs.actionMaps[0].FindAction("Right");
            codeAction = playerInputs.actionMaps[0].FindAction("Code");
            jumpAction = playerInputs.actionMaps[0].FindAction("Jump");
            pauseAction = playerInputs.actionMaps[0].FindAction("Pause");
        }
        else
        {
            Debug.Log("dummy");
        }
        logicFrame = 0;

        //bufferInput = InputConverter.ConvertFromLong(5);

        hitboxData = null;

        if (!GameManager.Instance.isOnlineMatchActive)
        {
            InitCharacter();
            ProjectileManager.Instance.InitializeAllProjectiles();
        }

        _playerPauseIndex = Array.IndexOf(GameManager.Instance.players, this);

    }

    private void Update()
    {
        UpdateToasts();
        UpdateDamageNumbers();
    }

    private void OnDisable()
    {
        ClearToasts();
        ClearDamageNumbers();

        //stop playing all repeating sounds for this player
        if (this.gameObject != null)
        {
            SFX_Manager.Instance.StopRepeatingPlayerSounds(Array.IndexOf(GameManager.Instance.players, this));
            StopHitRumble();
        }
    }

    private void OnDestroy()
    {
        ClearToasts();
        ClearDamageNumbers();

        if (this.gameObject != null)
        {
            //stop playing all repeating sounds for this player
            if (SFX_Manager.Instance != null) SFX_Manager.Instance.StopRepeatingPlayerSounds(Array.IndexOf(GameManager.Instance.players, this));
        }
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
        if(charData != null)
        {
            currentPlayerHealth = charData.playerHealth;
            runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
            slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
            maxJumpCount = (byte)charData.jumpCount;
            jumpForce = Fixed.FromInt(charData.jumpForce);
            playerWidth = Fixed.FromInt(charData.playerWidth);
            playerHeight = Fixed.FromInt(charData.playerHeight);

            startingSpell = charData.startingInventory[0];
        }
        

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
                playerIndexImages[0].enabled = true;
                playerIndexImages[0].color = GameManager.colors["red"];
                break;
            case 1:
                InitializePalette(matchPalette[1]);
                //playerNum.text = "P2";
                pID = 2;
                playerIndexImages[1].enabled = true;
                playerIndexImages[1].color = GameManager.colors["blue"];
                break;
            case 2:
                InitializePalette(matchPalette[2]);
                //playerNum.text = "P3";
                pID = 3;
                playerIndexImages[2].enabled = true;
                playerIndexImages[2].color = GameManager.colors["yellow"];
                break;
            case 3:
                InitializePalette(matchPalette[3]);
                //playerNum.text = "P4";
                pID = 4;
                playerIndexImages[3].enabled = true;
                playerIndexImages[3].color = GameManager.colors["green"];
                break;
            default:
                pID = 0;
                Vector2 spawnPosNPC = GameManager.Instance.GetNPCSpawnPositions()[0];
                FixedVec2 startPosNPC = FixedVec2.FromFloat(spawnPosNPC.x, spawnPosNPC.y);
                SpawnPlayer(startPosNPC);
                return;
                //break;
        }

        // Lock starter selection by PID using the actual dictionary keys.
        if (pID == 1) { startingSpell = "Amon Slash"; }
        else if (pID == 2) { startingSpell = "Quarter Report"; }
        else if (pID == 3) { startingSpell = "Blade Of Ares"; }
        else if (pID == 4) { startingSpell = "Skillshot Slash"; }

        FixedVec2 startPos;
        Vector2 spawnPos = GameManager.Instance.GetSpawnPositions()[Array.IndexOf(GameManager.Instance.players, this)];
        startPos = FixedVec2.FromFloat(spawnPos.x, spawnPos.y);
        SpawnPlayer(startPos);


    }

    public void SpawnPlayer(FixedVec2 spawnPos)
    {
        ClearToasts();
        isAlive = true;
        gameObject.GetComponent<SpriteRenderer>().enabled = true;
        input = InputConverter.ConvertFromLong(5);
        position = spawnPos;
        hSpd = Fixed.FromInt(0);
        vSpd = Fixed.FromInt(0);
        gravity = Fixed.FromFloat(0.75f);
        facingRight = spawnPos.X.RawValue <= 0;
        isGrounded = false;
        onPlatform = false;
        prevState = PlayerState.Idle;
        animationFrame = 0;
        lerpDelay = 0;
        stateSpecificArg = 0;
        hitstop = 0;
        hitstopActive = false;
        superArmor = false;
        comboCounter = 0;
        comboResetTimer = 0;
        armor = false;
        basicSpawnOverride = "";
        isHit = false;
        damageBarHitCount = 0;
        hitboxData = null;
        currentPlayerHealth = charData.playerHealth;
        runSpeed = Fixed.FromInt(charData.runSpeed) / Fixed.FromInt(10);
        slideSpeed = Fixed.FromInt(charData.slideSpeed) / Fixed.FromInt(10);
        jumpForce = Fixed.FromInt(charData.jumpForce);
        playerWidth = Fixed.FromInt(charData.playerWidth);
        playerHeight = Fixed.FromInt(charData.playerHeight);
        iframes = 180; //you get 3 sec of invul on spawn
        storedCode = 0;
        storedCodeDuration = 0;
        SetState(PlayerState.Idle);

        //play the spawning VFX
        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.SPAWN, position + FixedVec2.FromFloat(0f, 42f), pID);

        //stop playing blocking VFX
        VFX_Manager.Instance.StopVisualEffect(VisualEffects.BLOCKING, pID, true);

        //stop super armor VFX
        VFX_Manager.Instance.StopVisualEffect(VisualEffects.SUPER_ARMOR, pID, true);

        if(pID == 0)return;

        //initialize resources
        flowState = 0;
        //stockStability = GetPersistentStockStabilityFromSpellList();
        stockStability = 0;
        demonAura = 0;
        reps = 0;

        //call the load spell function for the starting spell to initialize the spell's variables and projectile data
        suppressSpellLoadSideEffects = true;
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null)
            {
                spellList[i].owner = this;
                spellList[i].LoadSpell();
            }
        }
        suppressSpellLoadSideEffects = false;
        CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnStart);
        GameManager.Instance.spellDisplays[Array.IndexOf(GameManager.Instance.players, this)].UpdateSpellDisplay(Array.IndexOf(GameManager.Instance.players, this));

    }


    

    public static int GetSpellInputLength(SpellData spell)
    {
        if (spell == null || spell.spellType != SpellType.Active)
        {
            return 0;
        }

        return (int)(spell.spellInput & 0xF);
    }

    public static int GetMaxCopiesForSpell(SpellData spell)
    {
        if (spell == null)
        {
            return 0;
        }

        if (spell.spellType == SpellType.Passive)
        {
            return 1;
        }

        int inputLength = Mathf.Max(1, GetSpellInputLength(spell));
        return Mathf.Max(1, 5 - inputLength);
    }

    public int GetSpellCountByName(string spellName)
    {
        int spellCount = 0;

        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null && spellList[i].spellName == spellName)
            {
                spellCount++;
            }
        }

        return spellCount;
    }

    public bool HasReachedSpellCopyLimit(string spellName)
    {
        if (SpellDictionary.Instance == null ||
            SpellDictionary.Instance.spellDict == null ||
            !SpellDictionary.Instance.spellDict.TryGetValue(spellName, out SpellData spellData) ||
            spellData == null)
        {
            return false;
        }

        return GetSpellCountByName(spellName) >= GetMaxCopiesForSpell(spellData);
    }

    public bool CanAddSpellToSpellList(string spellToAdd)
    {
        if (spellList.Count >= 6)
        {
            return false;
        }

        return !HasReachedSpellCopyLimit(spellToAdd);
    }

    public bool AddSpellToSpellList(string spellToAdd, bool applyLoadEffects = true)
    {
        if (spellList.Count >= 6)
        {
            Debug.LogWarning("Spell List Full, cannot add more spells!");
            return false;
        }

        if (SpellDictionary.Instance == null ||
            SpellDictionary.Instance.spellDict == null ||
            !SpellDictionary.Instance.spellDict.TryGetValue(spellToAdd, out SpellData targetSpell) ||
            targetSpell == null)
        {
            Debug.LogWarning("Spell not found in dictionary, cannot add!");
            return false;
        }

        if (HasReachedSpellCopyLimit(spellToAdd))
        {
            Debug.LogWarning($"Spell copy limit reached for {spellToAdd}, cannot add more copies!");
            return false;
        }

        SpellData spellInstance = Instantiate(targetSpell);
        spellList.Add(spellInstance);
        spellInstance.owner = this;
        if (!string.IsNullOrEmpty(startingSpell) && spellToAdd == startingSpell)
        {
            startingSpellAdded = true;
        }
        if (applyLoadEffects)
        {
            spellInstance.LoadSpell();
        }
        CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnStart);
        ProjectileManager.Instance.InitializeAllProjectiles();

        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);

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

        return true;
    }

    // private ushort GetPersistentStockStabilityFromSpellList()
    // {
    //     ushort totalStockStability = 0;

    //     for (int i = 0; i < spellList.Count; i++)
    //     {
    //         SpellData spell = spellList[i];
    //         if (spell == null) continue;

    //         switch (spell.spellName)
    //         {
    //             case "Quarter Report":
    //             case "Coin Toss":
    //             case "Get A Job":
    //             case "Penny Stock Peddler":
    //             case "Cash Out":
    //                 totalStockStability += 10;
    //                 break;
    //         }
    //     }

    //     return totalStockStability;
    // }


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
        startingSpellAdded = false;
        ProjectileManager.Instance.InitializeAllProjectiles();

        flowState = 0; //the timer for how long you are in flow state
        stockStability = 0; //percentage chance to crit before modifiers, e.g. 25 = 25% chance
        stockStabilityModified = 0; //crit chance after modifiers
        demonAura = 0;
        demonAuraLifeSpanTimer = 0;
        reps = 0;

    int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
    }

    public void RemoveSpellFromSpellList(string spellToRemove)
    {
        for (int i = 0; i < spellList.Count; i++)
        {
            if (spellList[i] != null && spellList[i].spellName == spellToRemove)
            {
                Destroy(spellList[i]);
                spellList.RemoveAt(i);
                startingSpellAdded = !string.IsNullOrEmpty(startingSpell)
                    && spellList.Exists(spell => spell != null && spell.spellName == startingSpell);
                ProjectileManager.Instance.InitializeAllProjectiles();

                int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
                GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
                return;
            }
        }
        Debug.LogWarning("Spell not found in spell list, cannot remove!");
    }

    public void AdjustIframeAndArmorVFX()
    {
        float targetBrightness = IsInvincible() ? 0.128f : 1.0f;
        MaterialPropertyBlock propertyBlock = new();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        if (propertyBlock.GetFloat("_Brightness") != targetBrightness)
        {
            propertyBlock.SetFloat("_Brightness", targetBrightness);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        //if this player has super armor,...
        if (superArmor)
        {
            //start playing the super armor VFX
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.SUPER_ARMOR, position, pID);
        }
        //else this player does NOT have super armor,...
        else
        {
            //stop playing the super armor VFX
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.SUPER_ARMOR, pID, true);
        }

        //if this player is blocking,...
        if (armor)
        {
            //start playing the blocking VFX
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BLOCKING, position, pID);
        }
        //else this player is NOT blocking,...
        else
        {
            //stop playing the blocking VFX
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.BLOCKING, pID, true);
        }

        ////if this player is blocking and the blockinf=g VFX is NOT playing,...
        //if (armor && !VFX_Manager.Instance.IsVisualEffecyPlaying(VisualEffects.SUPER_ARMOR, pID))
        //{
        //    //start playing the blocking VFX
        //    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.SUPER_ARMOR, position, pID);
        //}
        ////else this player is NOT blocking and the blocking VFX is playing,...
        //else if (!armor && VFX_Manager.Instance.IsVisualEffecyPlaying(VisualEffects.SUPER_ARMOR, pID))
        //{
        //    //stop playing the blocking VFX
        //    VFX_Manager.Instance.StopVisualEffect(VisualEffects.SUPER_ARMOR, pID, true);
        //}
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


    public void ResetSpellCooldowns()
    {
        foreach(SpellData spell in spellList)
        {
            spell.cooldownCounter = 0;
        }
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
    // private ulong GetRawKeyboardInput()
    // {
    //     // DEBUG: Check if ANY key is being pressed
    //     if (UnityEngine.Input.anyKey)
    //     {
    //         //Debug.LogWarning($"[GetRawKeyboardInput] SOME KEY IS PRESSED!");
    //         // Log specific keys
    //         //Debug.LogWarning($"W={UnityEngine.Input.GetKey(KeyCode.W)}, " +
    //         //                $"A={UnityEngine.Input.GetKey(KeyCode.A)}, " +
    //         //                $"S={UnityEngine.Input.GetKey(KeyCode.S)}, " +
    //         //                $"D={UnityEngine.Input.GetKey(KeyCode.D)}");
    //     }

    //     // Direction input (using numpad notation: 5 = neutral)
    //     bool up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
    //     bool down = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
    //     bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
    //     bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

    //     // Button states (need to track previous state for Pressed/Released detection)
    //     bool codeNow = UnityEngine.Input.GetKey(KeyCode.R);
    //     bool jumpNow = UnityEngine.Input.GetKey(KeyCode.T);

    //     // Store previous button states (you might need to add these as class fields)
    //     bool codePrev = codePrevFrame;
    //     bool jumpPrev = jumpPrevFrame;

    //     // Update for next frame
    //     codePrevFrame = codeNow;
    //     jumpPrevFrame = jumpNow;

    //     // Calculate direction (numpad notation)
    //     byte direction = 5; // neutral

    //     if (up && right) direction = 9;
    //     else if (up && left) direction = 7;
    //     else if (down && right) direction = 3;
    //     else if (down && left) direction = 1;
    //     else if (up) direction = 8;
    //     else if (down) direction = 2;
    //     else if (left) direction = 4;
    //     else if (right) direction = 6;

    //     // Calculate button states
    //     ButtonState codeState = GetButtonState(codePrev, codeNow);
    //     ButtonState jumpState = GetButtonState(jumpPrev, jumpNow);

    //     ButtonState[] buttons = new ButtonState[2] { codeState, jumpState };
    //     bool[] dirs = new bool[4] { up, down, left, right };

    //     //Debug.Log($"[GetRawKeyboardInput] Direction={direction}, Code={codeState}, Jump={jumpState}");

    //     // Convert to ulong using your existing converter
    //     return (ulong)InputConverter.ConvertToLong(buttons, dirs);
    // }

    // private bool codePrevFrame = false;
    // private bool jumpPrevFrame = false;

    // private ButtonState GetButtonState(bool previous, bool current)
    // {
    //     if (!previous && !current)
    //         return ButtonState.None;
    //     else if (current && !previous)
    //         return ButtonState.Pressed;
    //     else if (current && previous)
    //         return ButtonState.Held;
    //     else
    //         return ButtonState.Released;
    // }

    public void PlayerUpdate(ulong rawInput)
    {
        input = InputConverter.ConvertFromLong( pID == 0 ? 5 : (ulong)rawInput );

        // Pause logic
        Pause pause = GameManager.Instance.tempUI.gameObject.GetComponent<Pause>();
        if (!GameManager.Instance.isOnlineMatchActive)
        {
            if (input.ButtonStates[2] == ButtonState.Pressed)
            {
                pause.playerPauseIndex = _playerPauseIndex;

                if (pause.paused)
                {
                    pause.Resume();
                }
                else
                {
                    pause.Pausing();
                }
            }
        }

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

        //if the hurtboxgroup at your current logic frame and state has width and height of 0, then make the sprite renderer brighter to indicate invulnerability frames
        AdjustIframeAndArmorVFX();

        CheckHit(input);

        if(input.Direction <= 6)
        {
            tapJumpPrimed = true;
        }



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
        else
        {
            //if you are grounded, reset your jump count
            jumpCount = maxJumpCount;
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
                    if(input.Direction == 2 && onPlatform)
                    {
                        break;
                    }
                    SetState(PlayerState.Slide);
                    break;
                }

                if (jumpCount > 0 && (input.ButtonStates[1] == ButtonState.Pressed || ((tapJump? input.Direction > 6:false) && tapJumpPrimed)))
                {
                    DoJump();
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
                if (input.Direction < 4 && input.Direction != 2 && input.ButtonStates[1] == ButtonState.Pressed)
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
                else if (jumpCount > 0 && (input.ButtonStates[1] == ButtonState.Pressed || ((tapJump? input.Direction > 6:false) && tapJumpPrimed)))
                {
                    DoJump();
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
                if (vSpd > Fixed.FromInt(0) && input.ButtonStates[1] is ButtonState.Released or ButtonState.None && (tapJump?input.Direction <=6:true))
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
                    if(input.Direction == 2)break;
                    SetState(PlayerState.Slide);
                    break;
                }
                //air jump input check
                else if (jumpCount > 0 && (input.ButtonStates[1] == ButtonState.Pressed || ((tapJump? input.Direction > 6:false) && tapJumpPrimed)))
                {
                    
                    DoJump();
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
                            //Debug.Log("down input Pressed!");
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
                            //Debug.Log("left input Pressed!");
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
                            //Debug.Log("right input Pressed!");
                            //play the input right code sound
                            SFX_Manager.Instance.PlaySound(Sounds.INPUT_CODE_RIGHT, 1f, 1f);
                            break;
                        case 8:
                            stateSpecificArg |= (uint)(0b11 << (8 + (codeCount * 2)));
                            stateSpecificArg &= ~(1u << 4);
                            //Debug.Log("up input Pressed!");
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
                        if(storedCodeDuration == 0) SpawnToast($"{spellList[i].spellName.ToUpper()}!", GameManager.colors["white"]);
                        storedCodeDuration += 3;
                        if (toggleCodeInput? input.ButtonStates[0] is ButtonState.Pressed : input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                        {
                            //lightArmor = false;

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
                            armor = false;

                            //set the 5th bit to 0 to indicate we are no longer primed
                            stateSpecificArg &= ~(1u << 4);
                            //if the current code is a valid spell code, store it for later use
                            
                            storedCode = stateSpecificArg;

                            uint spellCodeLength = (storedCode & 0xFu);
                            storedCodeDuration = Math.Clamp(6 - spellCodeLength, 0, 6) * 60; //stored code lasts for 6 seconds (360 logic frames) minus 1 second (60 logic frames) per input in the code
                            SetState(isGrounded ? PlayerState.Idle : PlayerState.Jump);
                            SpawnToast("STORED!", GameManager.colors["white"]);
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
                    if (toggleCodeInput? input.ButtonStates[0] is ButtonState.Pressed : input.ButtonStates[0] is ButtonState.Released or ButtonState.None)
                    {
                        armor = false;

                        SetState(PlayerState.CodeRelease, stateSpecificArg);

                        break;
                    }
                    //jump button pressed
                    if (input.ButtonStates[1] == ButtonState.Pressed)
                    {
                        ClearInputDisplay();
                        stateSpecificArg = 0;
                        SpawnToast("INPUTS CLEARED!", GameManager.colors["white"]);
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
                    //uint testCode = stateSpecificArg & ~(1u << 4);
                    stateSpecificArg &= ~(1u << 4);
#region Secret Dev Codes
                    if (stateSpecificArg == 0b_0000_0000_0110_0110_0000_1111_0000_1000) //Konami Code input
                    {
                        Debug.Log("Konami Code Activated!");
                        if (!secretEpicPaletteActive)
                        {
                            SpawnToast("Hey Lois, I'm in Spell Code SlingerZ!", GameManager.colors["white"]);
                            InitializePalette(secretEpicPalette);
                            secretEpicPaletteActive = true;
                        }
                        else
                        {
                            InitializePalette(matchPalette[pID - 1]);
                            secretEpicPaletteActive = false;
                        }
                    }
                    if (stateSpecificArg == 0b_0000_0000_1001_1001_1111_0000_0000_1000) //Inverse Konami Code input
                    {
                        Debug.Log("Inverse Konami Code Activated!");
                        if (!secretNormalPaletteActive)
                        {
                            SpawnToast("I'm in Spell Code SlingerZ, Giggity!", GameManager.colors["white"]);
                            InitializePalette(secretNormalPalette);
                            secretNormalPaletteActive = true;
                        }
                        else
                        {
                            InitializePalette(matchPalette[pID - 1]);
                            secretNormalPaletteActive = false;
                        }
                    }
                    if (stateSpecificArg == 0b_0000_0000_0000_0000_0000_0000_0000_1100) //12 downs for relative inputs
                    {
                        Debug.Log("Relative Inputs activated!");
                        relativeInputs = !relativeInputs;
                        string activeWord = relativeInputs?"ACTIVATED":"DEACTIVATED";
                        SpawnToast($"RELATIVE INPUTS {activeWord}!", GameManager.colors["white"]);
                    }
                    if (stateSpecificArg == 0b_1111_1111_1111_1111_1111_1111_0000_1100) //12 Ups for toggle code input
                    {
                        Debug.Log("Toggle Code Input activated!");
                        toggleCodeInput = !toggleCodeInput;
                        string activeWord = toggleCodeInput?"ACTIVATED":"DEACTIVATED";
                        SpawnToast($"TOGGLE CODE INPUT {activeWord}!", GameManager.colors["white"]);
                    }
#endregion
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
                            inputDisplay.color = GameManager.colors["green"];

                            //play successful code cast sound
                            SFX_Manager.Instance.PlaySound(Sounds.EXIT_CODE_WEAVE);

                            //play the cast visual effect based on spell brand
                            switch (spellList[i].brands[0])
                            {
                                case Brand.VWave:
                                    //Play the cast visual effect depending on the direction the player is facing
                                    if (facingRight)
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_CAST, position + FixedVec2.FromFloat(24.5f, 45.5f), pID, facingRight);
                                    }
                                    else
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.VWAVE_CAST, position + FixedVec2.FromFloat(-24.5f, 45.5f), pID, facingRight);
                                    }
                                    break;
                                case Brand.DemonX:
                                    //Play the cast visual effect depending on the direction the player is facing
                                    if (facingRight)
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_CAST, position + FixedVec2.FromFloat(24.5f, 45.5f), pID, facingRight);
                                    }
                                    else
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMONX_CAST, position + FixedVec2.FromFloat(-24.5f, 45.5f), pID, facingRight);
                                    }
                                    break;
                                case Brand.BigStox:
                                    //Play the cast visual effect depending on the direction the player is facing
                                    if (facingRight)
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_CAST, position + FixedVec2.FromFloat(24.5f, 45.5f), pID, facingRight);
                                    }
                                    else
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BIGSTOX_CAST, position + FixedVec2.FromFloat(-24.5f, 45.5f), pID, facingRight);
                                    }
                                    break;
                                case Brand.Killeez:
                                    //Play the cast visual effect depending on the direction the player is facing
                                    if (facingRight)
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_CAST, position + FixedVec2.FromFloat(24.5f, 45.5f), pID, facingRight);
                                    }
                                    else
                                    {
                                        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.KILLEEZ_CAST, position + FixedVec2.FromFloat(-24.5f, 45.5f), pID, facingRight);
                                    }
                                    break;
                                default:
                                    break;
                            }

                            break;
                        }

                        if (spellList[i].spellInput == stateSpecificArg &&
                            spellList[i].spellType == SpellType.Active &&
                            spellList[i].cooldownCounter > 0)
                        {
                            inputDisplay.color = GameManager.colors["yellow"];
                            Debug.Log("COOLDOWN");
                            SpawnToast("ON COOLDOWN!",GameManager.colors["white"]);
                        }
                        else { inputDisplay.color = GameManager.colors["red"]; }
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

                    if (basicSpawnOverride == "")
                    {
                        //create an instance of your basic spell here
                        BaseProjectile newProjectile = ProjectileDictionary.Instance.projectileDict[charData.basicAttackProjId];
                        ProjectileManager.Instance.SpawnProjectile(charData.basicAttackProjId, this, facingRight, new FixedVec2(Fixed.FromInt(16), Fixed.FromInt(36)));
                        
                    }
                    else
                    {
                        basicSpawnOverride = "";
                    }

                    //basic spell is fired
                    basicsFired++;

                    //make input display flash red to indicate incorrect sequence

                    if (stateSpecificArg != 0)
                    {
                        //Play failed code weave sound
                        SFX_Manager.Instance.PlaySound(Sounds.FAILED_EXIT_CODE_WEAVE);

                        //Play the fail code visual effect depending on the direction the player is facing
                        if (facingRight)
                        {
                            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CODE_FAIL, position + FixedVec2.FromFloat(24.5f, 45.5f), pID, facingRight);
                        }
                        else
                        {
                            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CODE_FAIL, position + FixedVec2.FromFloat(-24.5f, 45.5f), pID, facingRight);
                        }
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
                if (jumpCount > 0 && (input.ButtonStates[1] == ButtonState.Pressed || ((tapJump? input.Direction > 6:false) && tapJumpPrimed)))
                {
                    DoJump();
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
            Debug.Log($"[DESYNC] f={GameManager.Instance.frameNumber} pid={pID} st={state} dir={input.Direction} b0={input.ButtonStates[0]} b1={input.ButtonStates[1]} b2={input.ButtonStates[2]} raw={rawInput} h={hSpd.RawValue} v={vSpd.RawValue} x={position.X.RawValue} y={position.Y.RawValue} g={isGrounded} plat={onPlatform} lerp={lerpDelay} lf={logicFrame}");
        }

        //handle player animation
        List<int> frameLengths = AnimationManager.Instance.GetFrameLengthsForCurrentState(this);
        animationFrame = GetCurrentFrameIndex(frameLengths, CharacterDataDictionary.GetAnimFrames(characterName, state).loopAnim);

        if(pID != 0)
        {
            int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
            GameManager.Instance.spellDisplays[playerIndex].UpdateCooldownDisplay(playerIndex);
        }
        

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
        StageDataSO stageDataSO = GameManager.Instance.currentStageIndex < 0 ? (GameManager.Instance.currentStageIndex == -1?GameManager.Instance.lobbySO: (GameManager.Instance.currentStageIndex == -2?GameManager.Instance.TutorialSO: GameManager.Instance.trainingGroundsSO)) : GameManager.Instance.stages[GameManager.Instance.currentStageIndex];
        //Debug.Log("stage: " + GameManager.Instance.currentStageIndex);
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
                    Fixed loopMinX = Fixed.FromFloat(stageDataSO.borderMin.x);
                    Fixed loopMaxX = Fixed.FromFloat(stageDataSO.borderMax.x);
                    Fixed loopMinY = Fixed.FromFloat(stageDataSO.borderMin.y);
                    Fixed loopMaxY = Fixed.FromFloat(stageDataSO.borderMax.y);

                    if (position.X > loopMaxX)
                    {
                        position = new FixedVec2(loopMinX, position.Y);
                        returnVal = true;
                    }
                    else if (position.X < loopMinX)
                    {
                        position = new FixedVec2(loopMaxX, position.Y);
                        returnVal = true;
                    }

                    if (position.Y > loopMaxY)
                    {
                        position = new FixedVec2(position.X, loopMinY);
                        returnVal = true;
                    }
                    else if (position.Y < loopMinY)
                    {
                        position = new FixedVec2(position.X, loopMaxY);
                        returnVal = true;
                    }
                    break;
                case BorderType.DeathZone:
                    Fixed dzMinX = Fixed.FromFloat(stageDataSO.borderMin.x);
                    Fixed dzMaxX = Fixed.FromFloat(stageDataSO.borderMax.x);
                    Fixed dzMinY = Fixed.FromFloat(stageDataSO.borderMin.y);
                    Fixed dzMaxY = Fixed.FromFloat(stageDataSO.borderMax.y);
                    if (position.X > dzMaxX || position.X < dzMinX ||
                        position.Y > dzMaxY || position.Y < dzMinY)
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
        superArmor = false;
    }

    private void DoJump()
    {
        vSpd = jumpForce;
        jumpCount--;
        tapJumpPrimed = false;
        CheckAllSpellConditionsOfProcCon(this,ProcCondition.OnJump);

        //play the jump sound
        SFX_Manager.Instance.PlaySound(Sounds.JUMP);

        //play the jump dust VFX
        VFX_Manager.Instance.PlayVisualEffect(VisualEffects.JUMP_DUST, position, pID, facingRight);

        SetState(PlayerState.Jump);
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


                armor = false;

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
                armor = true;
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
                GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex, true);

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

                //Play the slide SFX
                SFX_Manager.Instance.PlaySound(Sounds.SLIDE, 1.0f, 1.0f);

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
                GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex, false);
                gravity = Fixed.FromFloat(.75f);
                break;
            case PlayerState.CodeRelease:
                //stop continuously playing the code weave sound
                SFX_Manager.Instance.StopRepeatingSound(Sounds.CONTINUOUS_CODE_WEAVE, Array.IndexOf(GameManager.Instance.players, this));

                //turn off hitstun override when exiting code release in case we exited code release while still having hitstun override on from casting a spell
                armor = false;
                superArmor = false;
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
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.FLOW_STATE_AURA, position, pID, true, this.gameObject.transform, (float)flowState / (float)FlowState.maxFlowState * 100f);

            flowState--;
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.FLOW_STATE_AURA, pID);
        }



        if (demonAura > 0)
        {
            //now handled in Demon-X universal passive
            // if (demonAuraLifeSpanTimer > 0)
            // {
            //     demonAuraLifeSpanTimer--;
            // }
            // else
            // {
            //     demonAura = (ushort)Math.Clamp(demonAura - 1, 0, maxDemonAura);
            // }

            //Debug.Log("VFX Debugging | Player " + pID + "'s Demon Aura at " + (float)demonAura + ". And maxdemonAura at " + (float)maxDemonAura + ". And particle count at " + (((float)demonAura / (float)maxDemonAura) * 50f));

            //play the demon aura visual effect 
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEMON_AURA, position, pID, true, this.gameObject.transform, (((float)demonAura / (float)maxDemonAura) * 50f));
        }
        else
        {
            VFX_Manager.Instance.StopVisualEffect(VisualEffects.DEMON_AURA, pID);
        }



        //if (stockStabilityModified > 0)
        //{
        //    //play the stock aura visual effect 
        //    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.STOCK_AURA, position, pID, true, this.gameObject.transform, Mathf.Clamp(((float)stockStability / 100f), 0f, 1f) * 100f);
        //}
        //else
        //{
        //    VFX_Manager.Instance.StopVisualEffect(VisualEffects.STOCK_AURA, pID);
        //}



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
    public void TakeEffectDamage(int damageAmount, PlayerController attacker, Color? damageTextColor = null)
    {
        if (GameManager.Instance.currentStageIndex == -1)
        {
            //don't take damage in the lobby
            return;
        }

        if(damageTextColor == GameManager.colors["blue"])
        {
            //Play the critical hit VFX on top of the hit VFX
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.CRITICAL_HIT, position + FixedVec2.FromFloat(0f, 42f), pID);

            //Play the critical hit noise on top of the hit SFX
            SFX_Manager.Instance.PlaySound(Sounds.CRITICAL_HIT);
        }

        HandleDamage(attacker, damageAmount, damageTextColor);
    }

    public void CheckHit(InputSnapshot input)
    {
        if(iframes > 0)
        {
            iframes--;
            return;
        }
        // Check to see if hitboxData is not null if it's not null, that means the player has been attacked
        if (hitboxData != null /*&& isHit*/)
        {
            BaseProjectile sourceProjectile = hitboxData.parentProjectile;
            PlayerController attacker = sourceProjectile != null ? sourceProjectile.owner : null;
            //basically ignore hitstun so some other point in the player's logic can handle it uniquely (e.g. Stag Chi Special 2 parry)
            if (superArmor)
            {
                SpawnToast($"SUPER ARMORED!", GameManager.colors["white"]);

                //play the blocked sound
                SFX_Manager.Instance.PlaySound(Sounds.ARMOR_HIT, 1.0f, 1.0f);

                //Play the blocked visual effect
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BLOCKED, position, pID, facingRight);
                isHit = false;
                hitboxData = null;
                return;
            }

            //ignore hit if we are in codeweave and the attack level is less than 2 (basic attack)
            if (armor)
            {
                if(hitboxData.attackLvl < 2)
                {
                    SpawnToast($"ARMORED!", GameManager.colors["white"]);

                    //play the blocked sound
                    SFX_Manager.Instance.PlaySound(Sounds.ARMOR_HIT, 1.0f, 1.0f);

                    //Play the blocked visual effect
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BLOCKED, position, pID, facingRight);
                    isHit = false;
                    hitboxData = null;
                    return;
                }
                else
                {
                    SpawnToast($"ARMOR BREAK!", GameManager.colors["white"]);

                    //play armor shatter visual effect
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.ARMOR_BREAK, position + FixedVec2.FromFloat(0f, 42f), pID, facingRight);

                }
            }

            //mySFXHandler.PlaySound(SoundType.DAMAGED);

            if (GameManager.Instance.currentStageIndex ==-1)
            {
                //don't take damage in the lobby
                SpawnToast($"NO DAMAGE IN LOBBY!", GameManager.colors["white"]);
                isHit = false;
                hitboxData = null;
                return;
            }

            HandleDamage(attacker, hitboxData.damage);

            if(hitboxData.hitstun > 0)//this allows for things like D.O.T. A.O.E.s like morgana w
            {
                
                //ProjectileManager.Instance.DeleteAllPlayerProjectiles(pID);
                comboCounter++;
                if (comboCounter >= 4)
                {
                    SpawnToast("COMBO BREAK!!!", GameManager.colors["purple"]);
                    iframes = 120;
                    comboCounter = 0;

                    //Play the combo break VFX
                    VFX_Manager.Instance.PlayVisualEffect(VisualEffects.COMBO_BREAKER, position + FixedVec2.FromFloat(0f, 38f), pID);
                }
                //GameSessionManager.Instance.UpdatePlayerHealthText(Array.IndexOf(GameSessionManager.Instance.playerControllers, this));

            //play the damaged sound
            SFX_Manager.Instance.PlaySound(Sounds.HIT);

            //play the damage VFX
            VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DAMAGE, position + FixedVec2.FromFloat(0f, 42f), pID, facingRight);

            SetState(PlayerState.Hitstun);

            }
            
            
            


            
            //call the active on hit proc of the spell that created the projectile that hit us
            SpellData sourceSpell = sourceProjectile != null ? sourceProjectile.ownerSpell : null;
            if (sourceSpell == null)
            {
                sourceSpell = ResolveOnlineHitboxOwnerSpell(sourceProjectile);
            }
            if (sourceSpell != null)
            {
                sourceSpell.CheckCondition(this, ProcCondition.ActiveOnHit);
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

                //This logic is now handled in the Demon-X
                // if (attacker != null && attacker.demonAura > 0)
                // {
                //     attacker.demonAuraLifeSpanTimer = 360; //refresh demon aura lifespan timer on spell hit to 6 seconds (360 frames)
                // }
            }


            // if (GameManager.Instance.isOnlineMatchActive)
            // {
            //     isHit = false;
            //     hitboxData = null;
            // }
            isHit = false;
            hitboxData = null;

        }
    }
    private void HandleDamage(PlayerController attacker, int damageAmount, Color? damageTextColor = null)
    {
        //if(pID == 0)return; //if this is a training dummy then don't handle damage

        bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;
        bool hasAttacker = attacker != null;
        if (!isRollback && damageAmount > 0)
        {
            TriggerHitRumble(0.2f, 0.6f, 0.12f);
            SpawnDamageNumber(damageAmount, damageTextColor);
        }

        // Damage attribution is deterministic match state and must update during rollback replays too.
        if(pID != 0)
        {
            if (hasAttacker && damageAmount > 0)
            {
                GameManager.Instance.damageMatrix[pID - 1, attacker.pID - 1] += (byte)Math.Clamp(damageAmount, 0, currentPlayerHealth);
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
            }
        }
        

        //checking for death
        if (damageAmount >= currentPlayerHealth)
        {
            if (pID == 0)
            {
                currentPlayerHealth = charData.playerHealth;
                return;
            }
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

            //if all player bounties are 0,...
            if(GameManager.Instance.AllBountiesAreZero())
            {
                //play the death visual effect
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEATH, position + FixedVec2.FromFloat(0f, 42f), pID);
            }
            //else if this player is the one with the highest bounty,...
            else if(GameManager.Instance.GetPlayerWithHighestBounty() + 1 == pID)
            {
                //play the bounty death visual effect
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.BOUNTY_DEATH, position + FixedVec2.FromFloat(0f, 42f), pID);
            }
            //else this player is NOT the one with the highest bounty,...
            else
            {
                //play the death visual effect
                VFX_Manager.Instance.PlayVisualEffect(VisualEffects.DEATH, position + FixedVec2.FromFloat(0f, 42f), pID);
            }    

            CheckAllSpellConditionsOfProcCon(this, ProcCondition.OnDeath);

            currentPlayerHealth = 0;

            //award the killer with the extra bonus ram
            if (hasAttacker)
            {
                attacker.roundRam += baseRamKillBonus;
                attacker.totalRam += baseRamKillBonus;
                attacker.SpawnToast($"+{baseRamKillBonus} RAM", GameManager.colors["yellow"]);
            }

        }
        else
        {
            // Reduce health 
            currentPlayerHealth = (ushort)(currentPlayerHealth - (int)damageAmount);
        }
    }

    // Zero-allocation, deterministic replacement for the old
    //   list.Concat(universal).Where(s => s != null).OrderByDescending(s => s.priorityOverride).ToList()
    // which allocated four objects on every call and re-ran on every rollback resim. Refills the
    // reused 'buffer' in place. Uses a STABLE insertion sort so spells with equal priorityOverride
    // keep their original relative order (primary list first, then universal, each in order) --
    // byte-identical to LINQ's OrderByDescending. That order is part of deterministic match state
    // (proc-resolution priority), so it must not change.
    private static void BuildSortedSpellList(List<SpellData> primary, List<SpellData> universal, List<SpellData> buffer)
    {
        buffer.Clear();

        if (primary != null)
        {
            for (int i = 0; i < primary.Count; i++)
            {
                if (primary[i] != null) buffer.Add(primary[i]);
            }
        }
        if (universal != null)
        {
            for (int i = 0; i < universal.Count; i++)
            {
                if (universal[i] != null) buffer.Add(universal[i]);
            }
        }

        for (int i = 1; i < buffer.Count; i++)
        {
            SpellData current = buffer[i];
            int j = i - 1;
            // Shift only strictly-lower-priority spells right; stop at equal priority so
            // equal-priority spells keep insertion order (stable, matching OrderByDescending).
            while (j >= 0 && buffer[j].priorityOverride < current.priorityOverride)
            {
                buffer[j + 1] = buffer[j];
                j--;
            }
            buffer[j + 1] = current;
        }
    }

    /// <summary>
    /// This is a Helper function that checks all spells in the target player's spell list for the specified ProcCondition and calls their CheckCondition method.
    /// </summary>
    /// <param name="targetPlayer"></param>
    /// <param name="targetProcCon"></param>
    public void CheckAllSpellConditionsOfProcCon(PlayerController targetPlayer, ProcCondition targetProcCon)
    {
        BuildSortedSpellList(targetPlayer.spellList, targetPlayer.universalSpells, sortedSpellList);

        for (int i = 0; i < sortedSpellList.Count; i++)
        {
            if (sortedSpellList[i].procConditions.Contains(targetProcCon))
            {
                sortedSpellList[i].CheckCondition(this, targetProcCon);
            }
        }
    }

    private void EnsureUniversalSpells()
    {
        for (int i = 0; i < universalSpells.Count; i++)
        {
            if (universalSpells[i] != null)
            {
                universalSpells[i].owner = this;
            }
        }

        if (SpellDictionary.Instance == null || SpellDictionary.Instance.spellList == null)
        {
            return;
        }

        for (int i = 0; i < SpellDictionary.Instance.spellList.Count; i++)
        {
            SpellData universalSpell = SpellDictionary.Instance.spellList[i];
            if (universalSpell == null || universalSpell.spellType != SpellType.Universal)
            {
                continue;
            }

            bool alreadyAdded = universalSpells.Exists(spell => spell != null && spell.spellName == universalSpell.spellName);
            if (alreadyAdded)
            {
                continue;
            }

            SpellData spellInstance = Instantiate(universalSpell);
            spellInstance.owner = this;
            universalSpells.Add(spellInstance);
        }
    }

    private SpellData ResolveOnlineHitboxOwnerSpell(BaseProjectile sourceProjectile)
    {
        if (GameManager.Instance == null || !GameManager.Instance.isOnlineMatchActive)
        {
            return null;
        }

        if (sourceProjectile == null)
        {
            return null;
        }

        if (sourceProjectile.ownerSpell != null)
        {
            return sourceProjectile.ownerSpell;
        }

        PlayerController projectileOwner = sourceProjectile.owner;
        if (projectileOwner == null || projectileOwner.spellList == null)
        {
            return null;
        }

        for (int i = 0; i < projectileOwner.spellList.Count; i++)
        {
            SpellData spell = projectileOwner.spellList[i];
            if (spell == null || spell.projectileInstances == null)
            {
                continue;
            }

            for (int j = 0; j < spell.projectileInstances.Count; j++)
            {
                GameObject projectileInstance = spell.projectileInstances[j];
                if (projectileInstance == sourceProjectile.gameObject)
                {
                    sourceProjectile.ownerSpell = spell;
                    return spell;
                }
            }
        }

        return null;
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
        pauseButton[0] = pauseButton[1];

        codeButton[1] = codeAction.inProgress;
        jumpButton[1] = jumpAction.inProgress;
        pauseButton[1] = pauseAction.inProgress;

        buttons[0] = GetCurrentState(codeButton[0], codeButton[1]);
        buttons[1] = GetCurrentState(jumpButton[0], jumpButton[1]);
        buttons[2] = GetCurrentState(pauseButton[0], pauseButton[1]);
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
        bw.Write(superArmor);
        bw.Write(comboCounter);
        bw.Write(comboResetTimer);
        bw.Write(armor);
        bw.Write(GetSpellSerializationId(basicSpawnOverride));
        bw.Write(storedCode);
        bw.Write(storedCodeDuration);
        bw.Write(currentPlayerHealth);
        bw.Write(isAlive);
        bw.Write(isHit);
        bw.Write(damageBarHitCount);
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
        bw.Write(stockStabilityModified);
        bw.Write(demonAura);
        bw.Write(demonAuraLifeSpanTimer);
        bw.Write(reps);
        bw.Write(tapJump);
        bw.Write(jumpCount);
        bw.Write(maxJumpCount);
        //bw.Write(momentum);
        //bw.Write(slimed);
        bw.Write(isSpawned);
        bw.Write(roundsWon);
        bw.Write(totalRam);
        bw.Write(roundRam);
        bw.Write(ramBounty);
        bw.Write(chosenStartingSpell);
        bw.Write(startingSpellAdded);
        bw.Write(vWave);
        bw.Write(killeez);
        bw.Write(DemonX);
        bw.Write(bigStox);
        bw.Write(unchecked((int)0xAABBCCDD));


        // Spell List Serialization
        bw.Write(unchecked((int)0xAABBCCDD));
        bw.Write(spellList.Count);
        for (int i = 0; i < spellList.Count; i++)
        {
            SerializeSpellStateInline(bw, spellList[i]);
        }

        //bw.Write(InputConverter.ConvertFromInputSnapshot(bufferInput));
    }

    public void SerializeGameplayHash(BinaryWriter bw)
    {
        SerializeGameplayCoreHash(bw);

        bw.Write(spellList.Count);
        for (int i = 0; i < spellList.Count; i++)
        {
            SerializeSpellStateInline(bw, spellList[i]);
        }
    }

    public void SerializeGameplayCoreHash(BinaryWriter bw)
    {
        bw.Write(position.X.RawValue);
        bw.Write(position.Y.RawValue);
        bw.Write(hSpd.RawValue);
        bw.Write(vSpd.RawValue);
        bw.Write(facingRight);
        bw.Write(isGrounded);
        bw.Write(onPlatform);
        bw.Write(relativeInputs);
        bw.Write((byte)state);
        bw.Write(logicFrame);
        bw.Write(stateSpecificArg);
        bw.Write(hitstop);
        bw.Write(hitstopActive);
        bw.Write(superArmor);
        bw.Write(comboCounter);
        bw.Write(comboResetTimer);
        bw.Write(armor);
        bw.Write(GetSpellSerializationId(basicSpawnOverride));
        bw.Write(storedCode);
        bw.Write(storedCodeDuration);
        bw.Write(currentPlayerHealth);
        bw.Write(isAlive);
        bw.Write(isHit);
        bw.Write(damageBarHitCount);
        bw.Write(iframes);

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

        bw.Write(flowState);
        bw.Write(stockStability);
        bw.Write(stockStabilityModified);
        bw.Write(demonAura);
        bw.Write(demonAuraLifeSpanTimer);
        bw.Write(reps);
        bw.Write(tapJump);
        bw.Write(jumpCount);
        bw.Write(maxJumpCount);
    }

    public void SerializeGameplaySpellHash(BinaryWriter bw)
    {
        bw.Write(spellList.Count);
        for (int i = 0; i < spellList.Count; i++)
        {
            SerializeSpellStateInline(bw, spellList[i]);
        }
    }

    private static void SerializeSpellStateInline(BinaryWriter bw, SpellData spell)
    {
        bw.Write(GetSpellSerializationId(spell != null ? spell.spellName : null));

        Stream stream = bw.BaseStream;
        long lengthPosition = stream.Position;
        bw.Write(0);
        long dataStartPosition = stream.Position;

        if (spell != null)
        {
            spell.Serialize(bw);
        }

        long dataEndPosition = stream.Position;
        int dataLength = checked((int)(dataEndPosition - dataStartPosition));
        stream.Position = lengthPosition;
        bw.Write(dataLength);
        stream.Position = dataEndPosition;
    }

    private struct SavedSpellState
    {
        public int id;
        public long dataStart;
        public int dataLength;

        public SavedSpellState(int id, long dataStart, int dataLength)
        {
            this.id = id;
            this.dataStart = dataStart;
            this.dataLength = dataLength;
        }
    }

    // Spell strings in the serialization hot path are written as a stable int id (index
    // into SpellDictionary.spellList, identical on every client) instead of a length-prefixed
    // string, so a save-state / rollback no longer allocates a string per spell via ReadString.
    // -1 == "no spell" (e.g. an empty basicSpawnOverride). These thin wrappers null-guard the
    // dictionary and keep the Serialize/Deserialize pair symmetric.
    private static int GetSpellSerializationId(string spellName)
    {
        return SpellDictionary.Instance != null ? SpellDictionary.Instance.GetSpellId(spellName) : -1;
    }

    private static string GetSpellNameFromSerializationId(int spellId)
    {
        return SpellDictionary.Instance != null ? SpellDictionary.Instance.GetSpellName(spellId) : "";
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
        superArmor = br.ReadBoolean();
        comboCounter = br.ReadByte();
        comboResetTimer = br.ReadUInt16();
        armor = br.ReadBoolean();
        basicSpawnOverride = GetSpellNameFromSerializationId(br.ReadInt32());
        storedCode = br.ReadUInt32();
        storedCodeDuration = br.ReadUInt32();
        currentPlayerHealth = br.ReadUInt16();
        isAlive = br.ReadBoolean();
        isHit = br.ReadBoolean();
        damageBarHitCount = br.ReadUInt32();
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
            hitboxData.attackLvl = br.ReadInt32();
            hitboxData.basicAttackHitbox = br.ReadBoolean();
            _pendingHitboxOwnerIndex = br.ReadInt32();
            _pendingHitboxProjectileIndex = br.ReadInt32();
        }
        else
        {
            hitboxData = null;
            _pendingHitboxOwnerIndex = -1;
            _pendingHitboxProjectileIndex = -1;
        }

        int markerB = br.ReadInt32();
        if (markerB != unchecked((int)0xAABBCCDD)) Debug.LogError($"MISALIGN at B: {markerB:X8}");
        flowState = br.ReadUInt16();
        stockStability = br.ReadUInt16();
        stockStabilityModified = br.ReadUInt16();
        demonAura = br.ReadUInt16();
        demonAuraLifeSpanTimer = br.ReadUInt16();
        reps = br.ReadUInt16();
        tapJump = br.ReadBoolean();
        jumpCount = br.ReadByte();
        maxJumpCount = br.ReadByte();
        //momentum = br.ReadUInt16();
        //slimed = br.ReadBoolean();
        isSpawned = br.ReadBoolean();
        roundsWon = br.ReadInt32();
        totalRam = br.ReadUInt16();
        roundRam = br.ReadUInt16();
        ramBounty = br.ReadInt16();
        chosenStartingSpell = br.ReadBoolean();
        bool savedStartingSpellAdded = br.ReadBoolean();
        vWave = br.ReadBoolean();
        killeez = br.ReadBoolean();
        DemonX = br.ReadBoolean();
        bigStox = br.ReadBoolean();
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
                suppressSpellLoadSideEffects = true;
                AddSpellToSpellList(startingSpell, applyLoadEffects: true);
                suppressSpellLoadSideEffects = false;
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

        // Read serialized spell payload ranges first. Spell identity is now a stable int id
        // rather than a per-spell string, and payloads stay in the parent snapshot stream
        // instead of being copied to a fresh byte[] per spell.
        List<SavedSpellState> savedSpells = new List<SavedSpellState>(spellCount);
        for (int i = 0; i < spellCount; i++)
        {
            int spellId = br.ReadInt32();
            int spellDataLength = br.ReadInt32();
            long spellDataStart = br.BaseStream.Position;
            savedSpells.Add(new SavedSpellState(spellId, spellDataStart, spellDataLength));
            br.BaseStream.Position = spellDataStart + spellDataLength;
        }
        long spellPayloadEnd = br.BaseStream.Position;

        if (spellList.Count != spellCount)
        {
            // Online: this is expected when an authoritative snapshot is ahead of the
            // local sim (e.g. host snapshotted right after a floppy pickup but the local
            // sim hadn't reached that frame yet). RebuildSpellListFromSaved correctly
            // reconciles, so demote to LogWarning instead of LogError to reduce noise.
            Debug.LogWarning($"Spell list size mismatch during Deserialize. Expected {spellCount}, got {spellList.Count}. Rebuilding list from saved ids.");
            RebuildSpellListFromSaved(savedSpells);
        }

        // Deserialize spell state by saved order so duplicate spell names restore deterministically.
        for (int i = 0; i < savedSpells.Count; i++)
        {
            int spellId = savedSpells[i].id;
            int spellDataLength = savedSpells[i].dataLength;

            if (i >= spellList.Count || spellList[i] == null)
            {
                Debug.LogWarning($"Spell slot {i} missing while restoring id {spellId} - skipped {spellDataLength} bytes");
                continue;
            }

            SpellData spellInstance = spellList[i];
            if (GetSpellSerializationId(spellInstance.spellName) != spellId)
            {
                Debug.LogWarning($"Spell order mismatch at slot {i}. Expected id {spellId}, found '{spellInstance.spellName}'. Rebuilding from saved order.");
                RebuildSpellListFromSaved(savedSpells);
                if (i >= spellList.Count || spellList[i] == null)
                {
                    Debug.LogWarning($"Spell slot {i} still missing after rebuild for id {spellId} - skipped {spellDataLength} bytes");
                    continue;
                }
                spellInstance = spellList[i];
            }

            br.BaseStream.Position = savedSpells[i].dataStart;
            spellInstance.Deserialize(br);
            br.BaseStream.Position = savedSpells[i].dataStart + spellDataLength;
        }
        br.BaseStream.Position = spellPayloadEnd;
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
        BuildSortedSpellList(spellList, universalSpells, sortedSpellList);
        //go through the player's spell list and update any proc effects
        for (int i = 0; i < sortedSpellList.Count; i++)
        {
            sortedSpellList[i].SpellUpdate();
        }
    }
    public bool IsStorableState() =>
        state == PlayerState.Idle ||
        state == PlayerState.Run ||
        state == PlayerState.Jump ||
        state == PlayerState.Slide ||
        state == PlayerState.CodeWeave;

    // Spell-instance pooling for RebuildSpellListFromSaved, to avoid the
    // Destroy()/Instantiate() churn on the rollback hot path. Keyed by serialization id so a rented
    // instance is always the correct concrete spell type (its constructor-set constants stay valid).
    private void ReturnSpellInstanceToPool(SpellData spell)
    {
        if (spell == null) return;
        int id = SpellDictionary.Instance != null ? SpellDictionary.Instance.GetSpellId(spell.spellName) : -1;
        if (id < 0)
        {
            // Unknown spell type -- can't key it for safe reuse, so fall back to the old behaviour.
            Destroy(spell.gameObject);
            return;
        }
        if (!spellInstancePool.TryGetValue(id, out Stack<SpellData> pool))
        {
            pool = new Stack<SpellData>();
            spellInstancePool[id] = pool;
        }
        spell.gameObject.SetActive(false);
        pool.Push(spell);
    }

    private SpellData RentSpellInstanceFromPool(int spellId)
    {
        if (spellId < 0) return null;
        if (spellInstancePool.TryGetValue(spellId, out Stack<SpellData> pool))
        {
            while (pool.Count > 0)
            {
                SpellData spell = pool.Pop();
                if (spell != null) // skip any instance destroyed out from under the pool
                {
                    spell.gameObject.SetActive(true);
                    return spell;
                }
            }
        }
        return null;
    }

    private void RebuildSpellListFromSaved(List<SavedSpellState> savedSpells)
    {
        // Issue 2: return existing instances to the pool instead of destroying them.
        for (int i = spellList.Count - 1; i >= 0; i--)
        {
            SpellData spell = spellList[i];
            if (spell != null)
            {
                ReturnSpellInstanceToPool(spell);
            }
        }
        spellList.Clear();

        // Recreate list in saved order (no LoadSpell to avoid side effects). Reuse a pooled instance
        // of the same id when one exists; only Instantiate when the pool is empty for that id.
        for (int i = 0; i < savedSpells.Count; i++)
        {
            int spellId = savedSpells[i].id;
            SpellData instance = RentSpellInstanceFromPool(spellId);
            if (instance == null)
            {
                SpellData template = SpellDictionary.Instance != null
                    ? SpellDictionary.Instance.GetSpellTemplate(spellId)
                    : null;
                if (template == null)
                {
                    Debug.LogWarning($"RebuildSpellListFromSaved: Missing spell id {spellId} in dictionary.");
                    continue;
                }
                instance = Instantiate(template);
            }
            instance.owner = this;
            spellList.Add(instance);
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

        // Rebuild projectile pool once to match the new spell list.
        // Online-only: when this rebuild is happening inside a managed-state deserialize
        // (snapshot / rollback apply), route through GameManager so it can BATCH the pool
        // rebuild to a single call after every player's spell list is finalised. Outside
        // of deserialize, the request executes immediately, matching legacy behaviour.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestProjectilePoolRebuild();
        }
        else if (ProjectileManager.Instance != null)
        {
            ProjectileManager.Instance.InitializeAllProjectiles();
        }

        startingSpellAdded = !string.IsNullOrEmpty(startingSpell)
            && spellList.Exists(spell => spell != null && spell.spellName == startingSpell);

        // Update UI if available
        int playerIndex = Array.IndexOf(GameManager.Instance.players, this);
        if (playerIndex >= 0 && GameManager.Instance.spellDisplays != null &&
            playerIndex < GameManager.Instance.spellDisplays.Length &&
            GameManager.Instance.spellDisplays[playerIndex] != null)
        {
            GameManager.Instance.spellDisplays[playerIndex].UpdateSpellDisplay(playerIndex);
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

        if ((toggleCodeInput? input.ButtonStates[0] is ButtonState.Pressed : input.ButtonStates[0] is ButtonState.Released or ButtonState.None) || storedCodeDuration <= 0)
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

    public void CheckForInputs(bool enable, bool assignKeyboardOnly)
    {
        inputs.CheckForInputs(enable, assignKeyboardOnly);
    }

    public void ClearInputDisplay()
    {
        if ((RollbackManager.Instance != null && !RollbackManager.Instance.isRollbackFrame) || RollbackManager.Instance == null)
        {
            inputDisplay.text = "";
            inputDisplay.color = GameManager.colors["white"];
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

    public void SpawnDamageNumber(int damageAmount, Color? color = null)
    {
        if (damageAmount <= 0)
        {
            return;
        }

        SpawnDamageNumber(damageAmount.ToString(), color);
    }

    public void SpawnDamageNumber(string text, Color? color = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame)
        {
            return;
        }

        EnsureDamageNumberRoot();
        Color damageNumberColor = color ?? Color.white;

        GameObject damageNumberObject = new($"{name}_DamageNumber");
        damageNumberObject.transform.SetParent(damageNumberRoot, false);

        TextMeshPro damageNumberText = damageNumberObject.AddComponent<TextMeshPro>();
        damageNumberText.text = text;
        damageNumberText.color = damageNumberColor;
        damageNumberText.alignment = TextAlignmentOptions.Center;
        damageNumberText.fontSize = damageNumberFontSize;
        damageNumberText.fontStyle = FontStyles.Bold;
        damageNumberText.textWrappingMode = TextWrappingModes.NoWrap;
        damageNumberText.overflowMode = TextOverflowModes.Overflow;
        damageNumberText.raycastTarget = false;
        damageNumberText.sortingOrder = 100;

        Renderer damageNumberRenderer = damageNumberText.GetComponent<Renderer>();
        if (damageNumberRenderer != null)
        {
            damageNumberRenderer.sortingLayerID = GetFrontmostSortingLayerId();
            damageNumberRenderer.sortingOrder = short.MaxValue;
        }

        float horizontalDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float horizontalDistance = UnityEngine.Random.Range(damageNumberHorizontalDrift * 0.65f, damageNumberHorizontalDrift);
        float spawnJitter = UnityEngine.Random.Range(-8f, 8f);

        activeDamageNumbers.Add(new PlayerDamageNumber
        {
            textMesh = damageNumberText,
            elapsed = 0f,
            baseColor = damageNumberColor,
            startOffset = new Vector3(spawnJitter, damageNumberBaseVerticalOffset, 0f),
            drift = new Vector3(horizontalDirection * horizontalDistance, damageNumberRiseDistance, 0f)
        });

        UpdateDamageNumberVisuals();
    }

    public static string ConvertCodeToString(uint code, Color? color = null)
    {
        if (color == null) { color = GameManager.colors["white"]; }

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

    private void EnsureDamageNumberRoot()
    {
        if (damageNumberRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find("DamageNumberRoot");
        if (existingRoot != null)
        {
            damageNumberRoot = existingRoot;
        }
        else
        {
            GameObject damageNumberRootObject = new("DamageNumberRoot");
            damageNumberRoot = damageNumberRootObject.transform;
            damageNumberRoot.SetParent(transform, false);
        }

        damageNumberRoot.localPosition = new Vector3(0f, 0f, -0.1f);
        damageNumberRoot.localRotation = Quaternion.identity;
        damageNumberRoot.localScale = Vector3.one;
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

    private void UpdateDamageNumbers()
    {
        if (activeDamageNumbers.Count == 0)
        {
            return;
        }

        float lifetime = Mathf.Max(0.01f, damageNumberLifetime);
        for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
        {
            PlayerDamageNumber damageNumber = activeDamageNumbers[i];
            if (damageNumber == null || damageNumber.textMesh == null)
            {
                activeDamageNumbers.RemoveAt(i);
                continue;
            }

            damageNumber.elapsed += Time.deltaTime;
            if (damageNumber.elapsed >= lifetime)
            {
                Destroy(damageNumber.textMesh.gameObject);
                activeDamageNumbers.RemoveAt(i);
            }
        }

        if (activeDamageNumbers.Count == 0)
        {
            return;
        }

        UpdateDamageNumberVisuals();
    }

    private void UpdateDamageNumberVisuals()
    {
        float lifetime = Mathf.Max(0.01f, damageNumberLifetime);
        float fadeDuration = Mathf.Clamp(damageNumberFadeDuration, 0f, lifetime);
        float fadeStart = lifetime - fadeDuration;

        for (int i = 0; i < activeDamageNumbers.Count; i++)
        {
            PlayerDamageNumber damageNumber = activeDamageNumbers[i];
            if (damageNumber == null || damageNumber.textMesh == null)
            {
                continue;
            }

            float normalizedLifetime = Mathf.Clamp01(damageNumber.elapsed / lifetime);
            float alpha = damageNumber.baseColor.a;
            if (fadeDuration > 0f && damageNumber.elapsed > fadeStart)
            {
                float fadeProgress = Mathf.InverseLerp(fadeStart, lifetime, damageNumber.elapsed);
                alpha *= 1f - fadeProgress;
            }

            Color displayColor = damageNumber.baseColor;
            displayColor.a = alpha;
            damageNumber.textMesh.color = displayColor;

            float popScale = Mathf.Lerp(0.8f, 1.15f, Mathf.Clamp01(normalizedLifetime / 0.18f));
            float settleScale = Mathf.Lerp(popScale, 0.95f, Mathf.Clamp01((normalizedLifetime - 0.18f) / 0.82f));
            damageNumber.textMesh.transform.localScale = Vector3.one * settleScale;

            float easedMovement = 1f - Mathf.Pow(1f - normalizedLifetime, 2f);
            float floatBob = Mathf.Sin(normalizedLifetime * Mathf.PI) * 8f;
            float gravityFall = damageNumberGravityFallDistance * normalizedLifetime * normalizedLifetime;
            damageNumber.textMesh.transform.localPosition = damageNumber.startOffset + (damageNumber.drift * easedMovement) + new Vector3(0f, floatBob - gravityFall, 0f);
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

    private void ClearDamageNumbers()
    {
        for (int i = activeDamageNumbers.Count - 1; i >= 0; i--)
        {
            PlayerDamageNumber damageNumber = activeDamageNumbers[i];
            if (damageNumber != null && damageNumber.textMesh != null)
            {
                Destroy(damageNumber.textMesh.gameObject);
            }
        }

        activeDamageNumbers.Clear();
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

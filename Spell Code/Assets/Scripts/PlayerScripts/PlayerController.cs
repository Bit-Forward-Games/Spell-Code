using System.Collections.Generic;
using System;
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
    private InputAction lightAction;
    private InputAction mediumAction;
    private InputAction heavyAction;
    private InputAction specialAction;
    private readonly bool[] direction = new bool[4];
    private readonly bool[] codeButton = new bool[2];
    private readonly bool[] jumpButton = new bool[2];
    private readonly ButtonState[] buttons = new ButtonState[2];
    public InputSnapshot input;
    //public InputSnapshot bufferInput;
    public string characterName = "Slugmancer";

    [HideInInspector]
    public List<int> cancelOptions = new();
    //MOVEMENT
    private const int knockdownFrames = 26;

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
}

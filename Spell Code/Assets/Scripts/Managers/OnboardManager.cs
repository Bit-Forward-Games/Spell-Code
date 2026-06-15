using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnboardManager : MonoBehaviour
{
    //variables
    public InputSnapshot[] inputSnapshots = new InputSnapshot[4];

    public static OnboardManager Instance { get; private set; }

    private GameManager gM;

    public Sprite inputGraphic;
    public Sprite atkGraphic;

    [Header("Player 1")]
    public bool p1_moveComplete = false;
    public bool p1_jumpComplete = false;
    public bool p1_atkComplete = false;
    public bool p1_glassBroken = false;
    private bool p1_onlineJoined = true;

    public Image p1_moveGraphic;
    public TextMeshProUGUI p1_moveTxt;
    public Image p1_jumpGraphic;
    public TextMeshProUGUI p1_jumpTxt;
    public Image p1_atkGraphic;
    public TextMeshProUGUI p1_atkTxt;
    public Image p1_spellSlctGraphic;
    public TextMeshProUGUI p1_castTxt;
    public Image p1_castGraphic;
    public SpriteRenderer p1_breakWSpellcode;

    public GambaMachine p1_gamba;
    private bool p1_gambaActive;

    [Header("Player 2")]
    public bool p2_joined = false;
    public bool p2_moveComplete = false;
    public bool p2_jumpComplete = false;
    public bool p2_atkComplete = false;
    public bool p2_glassBroken = false;

    public Image p2_moveGraphic;
    public TextMeshProUGUI p2_moveTxt;
    public Image p2_jumpGraphic;
    public TextMeshProUGUI p2_jumpTxt;
    public Image p2_atkGraphic;
    public TextMeshProUGUI p2_atkTxt;
    public Image p2_spellSlctGraphic;
    public TextMeshProUGUI p2_castTxt;
    public Image p2_castGraphic;
    public SpriteRenderer p2_breakWSpellcode;

    public GambaMachine p2_gamba;
    private bool p2_gambaActive;

    [Header("Player 3")]
    public bool p3_joined = false;
    public bool p3_moveComplete = false;
    public bool p3_jumpComplete = false;
    public bool p3_atkComplete = false;
    public bool p3_glassBroken = false;

    public Image p3_moveGraphic;
    public TextMeshProUGUI p3_moveTxt;
    public Image p3_jumpGraphic;
    public TextMeshProUGUI p3_jumpTxt;
    public Image p3_atkGraphic;
    public TextMeshProUGUI p3_atkTxt;
    public Image p3_spellSlctGraphic;
    public TextMeshProUGUI p3_castTxt;
    public Image p3_castGraphic;
    public SpriteRenderer p3_breakWSpellcode;

    public GambaMachine p3_gamba;
    private bool p3_gambaActive;

    [Header("Player 4")]
    public bool p4_joined = false;
    public bool p4_moveComplete = false;
    public bool p4_jumpComplete = false;
    public bool p4_atkComplete = false;
    public bool p4_glassBroken = false;

    public Image p4_moveGraphic;
    public TextMeshProUGUI p4_moveTxt;
    public Image p4_jumpGraphic;
    public TextMeshProUGUI p4_jumpTxt;
    public Image p4_atkGraphic;
    public TextMeshProUGUI p4_atkTxt;
    public Image p4_spellSlctGraphic;
    public TextMeshProUGUI p4_castTxt;
    public Image p4_castGraphic;
    public SpriteRenderer p4_breakWSpellcode;

    public GambaMachine p4_gamba;
    private bool p4_gambaActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gM = GameManager.Instance;
        ResetOnboarding();
    }

    public void ResetOnboarding()
    {
        if (!HasRequiredUiReferences())
        {
            Debug.LogWarning("[OnboardManager] Missing MainMenu UI references; disabling onboarding for this scene.");
            enabled = false;
            return;
        }

        ResetOnboardingFlags();
        ApplyInitialUiState();
    }

    public void ResetOnlineOnboarding()
    {
        if (!HasRequiredUiReferences())
        {
            Debug.LogWarning("[OnboardManager] Some MainMenu onboarding references are missing; continuing online onboarding with available references.");
        }

        enabled = true;
        ResetOnboardingFlags();
        ApplyInitialOnlineUiState();
    }

    private void ResetOnboardingFlags()
    {
        p1_onlineJoined = true;
        p1_moveComplete = false;
        p1_jumpComplete = false;
        p1_atkComplete = false;
        p1_glassBroken = false;

        p2_joined = false;
        p2_moveComplete = false;
        p2_jumpComplete = false;
        p2_atkComplete = false;
        p2_glassBroken = false;

        p3_joined = false;
        p3_moveComplete = false;
        p3_jumpComplete = false;
        p3_atkComplete = false;
        p3_glassBroken = false;

        p4_joined = false;
        p4_moveComplete = false;
        p4_jumpComplete = false;
        p4_atkComplete = false;
        p4_glassBroken = false;
    }

    private void ApplyInitialUiState()
    {
        //properly set starting states for UI components
        p1_atkGraphic.enabled = false;
        p1_atkTxt.enabled = false;
        //p1_spellSlctGraphic.enabled = false;
        p1_castGraphic.enabled = false;
        p1_castTxt.enabled = false;
        p1_gambaActive = false;
        p1_breakWSpellcode.enabled = false;
        if (p1_gamba != null) p1_gamba.isActive = false;

        p2_atkTxt.text = "Join";
        p2_moveGraphic.enabled = false;
        p2_moveTxt.enabled = false;
        p2_jumpGraphic.enabled = false;
        p2_jumpTxt.enabled = false;
        //p2_spellSlctGraphic.enabled = false;
        p2_castGraphic.enabled = false;
        p2_castTxt.enabled = false;
        p2_gambaActive = false;
        p2_breakWSpellcode.enabled = false;
        if (p2_gamba != null) p2_gamba.isActive = false;

        p3_atkTxt.text = "Join";
        p3_moveGraphic.enabled = false;
        p3_moveTxt.enabled = false;
        p3_jumpGraphic.enabled = false;
        p3_jumpTxt.enabled = false;
        //p3_spellSlctGraphic.enabled = false;
        p3_castGraphic.enabled = false;
        p3_castTxt.enabled = false;
        p3_gambaActive = false;
        p3_breakWSpellcode.enabled = false;
        if (p3_gamba != null) p3_gamba.isActive = false;

        p4_atkTxt.text = "Join";
        p4_moveGraphic.enabled = false;
        p4_moveTxt.enabled = false;
        p4_jumpGraphic.enabled = false;
        p4_jumpTxt.enabled = false;
        //p4_spellSlctGraphic.enabled = false;
        p4_castGraphic.enabled = false;
        p4_castTxt.enabled = false;
        p4_gambaActive = false;
        p4_breakWSpellcode.enabled = false;
        if (p4_gamba != null) p4_gamba.isActive = false;
    }

    private void ApplyInitialOnlineUiState()
    {
        SetEnabled(p1_atkGraphic, false);
        SetEnabled(p1_atkTxt, false);
        SetEnabled(p1_castGraphic, false);
        SetEnabled(p1_castTxt, false);
        p1_gambaActive = false;
        SetEnabled(p1_breakWSpellcode, false);
        if (p1_gamba != null) p1_gamba.isActive = false;

        SetText(p2_atkTxt, "Join");
        SetEnabled(p2_moveGraphic, false);
        SetEnabled(p2_moveTxt, false);
        SetEnabled(p2_jumpGraphic, false);
        SetEnabled(p2_jumpTxt, false);
        SetEnabled(p2_castGraphic, false);
        SetEnabled(p2_castTxt, false);
        p2_gambaActive = false;
        SetEnabled(p2_breakWSpellcode, false);
        if (p2_gamba != null) p2_gamba.isActive = false;

        SetText(p3_atkTxt, "Join");
        SetEnabled(p3_moveGraphic, false);
        SetEnabled(p3_moveTxt, false);
        SetEnabled(p3_jumpGraphic, false);
        SetEnabled(p3_jumpTxt, false);
        SetEnabled(p3_castGraphic, false);
        SetEnabled(p3_castTxt, false);
        p3_gambaActive = false;
        SetEnabled(p3_breakWSpellcode, false);
        if (p3_gamba != null) p3_gamba.isActive = false;

        SetText(p4_atkTxt, "Join");
        SetEnabled(p4_moveGraphic, false);
        SetEnabled(p4_moveTxt, false);
        SetEnabled(p4_jumpGraphic, false);
        SetEnabled(p4_jumpTxt, false);
        SetEnabled(p4_castGraphic, false);
        SetEnabled(p4_castTxt, false);
        p4_gambaActive = false;
        SetEnabled(p4_breakWSpellcode, false);
        if (p4_gamba != null) p4_gamba.isActive = false;
    }

    public void OnboardUpdate(ulong[] playerInputs)
    {
        if (!enabled || !HasRequiredUiReferences())
        {
            return;
        }

        for (int i = 0; i < playerInputs.Length; i++)
        {
            inputSnapshots[i] = InputConverter.ConvertFromLong(playerInputs[i]);
        }

        //p1
        if (gM.players[0] != null)
        {
            //if player moves left or right
            if (!p1_moveComplete)
            {
                if (inputSnapshots[0].Direction == 4 || inputSnapshots[0].Direction == 6) { p1_moveComplete = true; p1_moveTxt.color = GameManager.colors["green"]; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p1_jumpComplete)
            {
                if (inputSnapshots[0].ButtonStates[1] == ButtonState.Pressed) { p1_jumpComplete = true; p1_jumpTxt.color = GameManager.colors["green"]; Debug.Log("Jump Onboard Complete"); }
            }

            //if move is done and player has not yet attacked
            if (p1_moveComplete && p1_jumpComplete && !p1_atkComplete)
            {
                p1_moveGraphic.enabled = false;
                p1_moveTxt.enabled = false;
                p1_jumpGraphic.enabled = false;
                p1_jumpTxt.enabled = false;

                p1_atkGraphic.enabled = true;
                p1_atkTxt.enabled = true;

                if (!p1_gambaActive) 
                { 
                    p1_gamba.isActive = true; 
                    p1_gambaActive = true; 
                }

                if (gM.players[0].basicsFired > 0) 
                { 
                    p1_atkComplete = true; 
                    Debug.Log("Atk Onboard Complete"); 
                }
            }

            //activate the floppy disk & let player get starting spell
            if (p1_atkComplete && gM.players[0].spellList.Count == 0)
            {
                p1_atkGraphic.enabled = false;
                p1_atkTxt.enabled = false;

                p1_moveGraphic.enabled = false;
                p1_moveTxt.enabled = false;
                p1_jumpGraphic.enabled = false;
                p1_jumpTxt.enabled = false;

            }
            //hold atk and input code to break free
            if (gM.players[0].spellList.Count>0 && !p1_glassBroken)
            {
                p1_castTxt.enabled = true;
                p1_castGraphic.enabled = true;
                p1_atkGraphic.enabled = false;
                p1_atkTxt.enabled = false;

                p1_breakWSpellcode.enabled = true;

                if (inputSnapshots[0].ButtonStates[0] == ButtonState.Held)
                {
                    p1_castGraphic.sprite = inputGraphic;
                    p1_castTxt.text = "Input";
                }
                else
                {
                    p1_castTxt.text = "Hold";
                    p1_castGraphic.sprite = atkGraphic;
                }

                if (gM.gates[0].isOpen == true)
                {
                    p1_glassBroken = true;
                }
            }

            if (p1_glassBroken)
            {
                p1_castTxt.color = GameManager.colors["green"];
                p1_castTxt.enabled = false;
                p1_castGraphic.enabled = false;

                p1_breakWSpellcode.enabled = false;
            }
        }

        //p2
        if (gM.players[1] != null)
        {
            if (!p2_joined)
            {
                p2_atkTxt.text = "Attack";
                p2_atkTxt.enabled = false;
                p2_atkGraphic.enabled = false;
                p2_moveGraphic.enabled = true;
                p2_moveTxt.enabled = true;
                p2_jumpGraphic.enabled = true;
                p2_jumpTxt.enabled = true;

                p2_joined = true;
            }

            //if player moves left or right
            if (!p2_moveComplete)
            {
                if (inputSnapshots[1].Direction == 4 || inputSnapshots[1].Direction == 6) { p2_moveComplete = true; p2_moveTxt.color = GameManager.colors["green"]; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p2_jumpComplete)
            {
                if (inputSnapshots[1].ButtonStates[1] == ButtonState.Pressed) { p2_jumpComplete = true; p2_jumpTxt.color = GameManager.colors["green"]; Debug.Log("Jump Onboard Complete"); }
            }
            
            //if move is done and player has not yet attacked
            if (p2_moveComplete && p2_jumpComplete && !p2_atkComplete)
            {
                p2_moveGraphic.enabled = false;
                p2_moveTxt.enabled = false;
                p2_jumpGraphic.enabled = false;
                p2_jumpTxt.enabled = false;

                p2_atkGraphic.enabled = true;
                p2_atkTxt.enabled = true;

                if (!p2_gambaActive)
                {
                    p2_gamba.isActive = true;
                    p2_gambaActive = true;
                }

                if (gM.players[1].basicsFired > 0)
                {
                    p2_atkComplete = true;
                    Debug.Log("Atk Onboard Complete");
                }
            }

            //activate the floppy disk & let player get starting spell
            if (p2_atkComplete && gM.players[1].spellList.Count == 0)
            {
                p2_atkGraphic.enabled = false;
                p2_atkTxt.enabled = false;

                p2_moveGraphic.enabled = false;
                p2_moveTxt.enabled = false;
                p2_jumpGraphic.enabled = false;
                p2_jumpTxt.enabled = false;
            }

            //hold atk and input code to break free
            if (gM.players[1].spellList.Count > 0 && !p2_glassBroken)
            {
                p2_castTxt.enabled = true;
                p2_castGraphic.enabled = true;
                p2_atkGraphic.enabled = false;
                p2_atkTxt.enabled = false;

                p2_breakWSpellcode.enabled = true;

                if (inputSnapshots[1].ButtonStates[0] == ButtonState.Held)
                {
                    p2_castGraphic.sprite = inputGraphic;
                    p2_castTxt.text = "Input";
                }
                else
                {
                    p2_castTxt.text = "Hold";
                    p2_castGraphic.sprite = atkGraphic;
                }

                if (gM.gates[1].isOpen == true)
                {
                    p2_glassBroken = true;
                    p2_castTxt.enabled = false;
                    p2_castGraphic.enabled = false;
                }
            }

            if (p2_glassBroken)
            {
                p2_castTxt.color = GameManager.colors["green"];
                p2_castTxt.enabled = false;
                p2_castGraphic.enabled = false;

                p2_breakWSpellcode.enabled = false;
            }
        }

        //p3
        if (gM.players[2] != null)
        {
            if (!p3_joined)
            {
                p3_atkTxt.text = "Attack";
                p3_atkTxt.enabled = false;
                p3_atkGraphic.enabled = false;
                p3_moveGraphic.enabled = true;
                p3_moveTxt.enabled = true;
                p3_jumpGraphic.enabled = true;
                p3_jumpTxt.enabled = true;

                p3_joined = true;
            }

            //if player moves left or right
            if (!p3_moveComplete)
            {
                if (inputSnapshots[2].Direction == 4 || inputSnapshots[2].Direction == 6) { p3_moveComplete = true; p3_moveTxt.color = GameManager.colors["green"]; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p3_jumpComplete)
            {
                if (inputSnapshots[2].ButtonStates[1] == ButtonState.Pressed) { p3_jumpComplete = true; p3_jumpTxt.color = GameManager.colors["green"]; Debug.Log("Jump Onboard Complete"); }
            }

            //if move is done and player has not yet attacked
            if (p3_moveComplete && p3_jumpComplete && !p3_atkComplete)
            {
                p3_moveGraphic.enabled = false;
                p3_moveTxt.enabled = false;
                p3_jumpGraphic.enabled = false;
                p3_jumpTxt.enabled = false;

                p3_atkGraphic.enabled = true;
                p3_atkTxt.enabled = true;

                if (!p3_gambaActive)
                {
                    p3_gamba.isActive = true;
                    p3_gambaActive = true;
                }

                if (gM.players[2].basicsFired > 0)
                {
                    p3_atkComplete = true;
                    Debug.Log("Atk Onboard Complete");
                }
            }

            //activate the floppy disk & let player get starting spell
            if (p3_atkComplete && gM.players[2].spellList.Count == 0)
            {
                p3_atkGraphic.enabled = false;
                p3_atkTxt.enabled = false;

                p3_moveGraphic.enabled = false;
                p3_moveTxt.enabled = false;
                p3_jumpGraphic.enabled = false;
                p3_jumpTxt.enabled = false;
            }

            //hold atk and input code to break free
            if (gM.players[2].spellList.Count > 0 && !p3_glassBroken)
            {
                p3_castTxt.enabled = true;
                p3_castGraphic.enabled = true;
                p3_atkGraphic.enabled = false;
                p3_atkTxt.enabled = false;

                p3_breakWSpellcode.enabled = true;

                if (inputSnapshots[2].ButtonStates[0] == ButtonState.Held)
                {
                    p3_castGraphic.sprite = inputGraphic;
                    p3_castTxt.text = "Input";
                }
                else
                {
                    p3_castTxt.text = "Hold";
                    p3_castGraphic.sprite = atkGraphic;
                }

                if (gM.gates[2].isOpen == true)
                {
                    p3_glassBroken = true;
                }
            }

            if (p3_glassBroken)
            {
                p3_castTxt.color = GameManager.colors["green"];
                p3_castTxt.enabled = false;
                p3_castGraphic.enabled = false;

                p3_breakWSpellcode.enabled = false;
            }
        }

        //p4
        if (gM.players[3] != null)
        {
            if (!p4_joined)
            {
                p4_atkTxt.text = "Attack";
                p4_atkTxt.enabled = false;
                p4_atkGraphic.enabled = false;
                p4_moveGraphic.enabled = true;
                p4_moveTxt.enabled = true;
                p4_jumpGraphic.enabled = true;
                p4_jumpTxt.enabled = true;

                p4_joined = true;
            }

            //if player moves left or right
            if (!p4_moveComplete)
            {
                if (inputSnapshots[3].Direction == 4 || inputSnapshots[3].Direction == 6) { p4_moveComplete = true; p4_moveTxt.color = GameManager.colors["green"]; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p4_jumpComplete)
            {
                if (inputSnapshots[3].ButtonStates[1] == ButtonState.Pressed) { p4_jumpComplete = true; p4_jumpTxt.color = GameManager.colors["green"]; Debug.Log("Jump Onboard Complete"); }
            }

            //if move is done and player has not yet attacked
            if (p4_moveComplete && p4_jumpComplete && !p4_atkComplete)
            {
                p4_moveGraphic.enabled = false;
                p4_moveTxt.enabled = false;
                p4_jumpGraphic.enabled = false;
                p4_jumpTxt.enabled = false;

                p4_atkGraphic.enabled = true;
                p4_atkTxt.enabled = true;

                if (!p4_gambaActive)
                {
                    p4_gamba.isActive = true;
                    p4_gambaActive = true;
                }

                if (gM.players[3].basicsFired > 0)
                {
                    p4_atkComplete = true;
                    Debug.Log("Atk Onboard Complete");
                }
            }

            //activate the floppy disk & let player get starting spell
            if (p4_atkComplete && gM.players[3].spellList.Count == 0)
            {
                p4_atkGraphic.enabled = false;
                p4_atkTxt.enabled = false;

                p4_moveGraphic.enabled = false;
                p4_moveTxt.enabled = false;
                p4_jumpGraphic.enabled = false;
                p4_jumpTxt.enabled = false;
            }

            //hold atk and input code to break free
            if (gM.players[3].spellList.Count > 0 && !p4_glassBroken)
            {
                p4_castTxt.enabled = true;
                p4_castGraphic.enabled = true;
                p4_atkGraphic.enabled = false;
                p4_atkTxt.enabled = false;

                p4_breakWSpellcode.enabled = true;

                if (inputSnapshots[3].ButtonStates[0] == ButtonState.Held)
                {
                    p4_castGraphic.sprite = inputGraphic;
                    p4_castTxt.text = "Input";
                }
                else
                {
                    p4_castTxt.text = "Hold";
                    p4_castGraphic.sprite = atkGraphic;
                }

                if (gM.gates[3].isOpen == true)
                {
                    p4_glassBroken = true;
                }
            }

            if (p4_glassBroken)
            {
                p4_castTxt.color = GameManager.colors["green"];
                p4_castTxt.enabled = false;
                p4_castGraphic.enabled = false;

                p4_breakWSpellcode.enabled = false;
            }
        }
    }

    public void OnboardUpdateOnline(ulong[] playerInputs)
    {
        if (!enabled || gM == null)
        {
            return;
        }

        int inputCount = Mathf.Min(playerInputs.Length, inputSnapshots.Length);
        for (int i = 0; i < inputCount; i++)
        {
            inputSnapshots[i] = InputConverter.ConvertFromLong(playerInputs[i]);
        }

        UpdatePlayerOnboardingOnline(0, false, ref p1_onlineJoined, ref p1_moveComplete, ref p1_jumpComplete, ref p1_atkComplete,
            ref p1_glassBroken, p1_moveGraphic, p1_moveTxt, p1_jumpGraphic, p1_jumpTxt, p1_atkGraphic, p1_atkTxt,
            p1_castTxt, p1_castGraphic, p1_breakWSpellcode, p1_gamba, ref p1_gambaActive);
        UpdatePlayerOnboardingOnline(1, true, ref p2_joined, ref p2_moveComplete, ref p2_jumpComplete, ref p2_atkComplete,
            ref p2_glassBroken, p2_moveGraphic, p2_moveTxt, p2_jumpGraphic, p2_jumpTxt, p2_atkGraphic, p2_atkTxt,
            p2_castTxt, p2_castGraphic, p2_breakWSpellcode, p2_gamba, ref p2_gambaActive);
        UpdatePlayerOnboardingOnline(2, true, ref p3_joined, ref p3_moveComplete, ref p3_jumpComplete, ref p3_atkComplete,
            ref p3_glassBroken, p3_moveGraphic, p3_moveTxt, p3_jumpGraphic, p3_jumpTxt, p3_atkGraphic, p3_atkTxt,
            p3_castTxt, p3_castGraphic, p3_breakWSpellcode, p3_gamba, ref p3_gambaActive);
        UpdatePlayerOnboardingOnline(3, true, ref p4_joined, ref p4_moveComplete, ref p4_jumpComplete, ref p4_atkComplete,
            ref p4_glassBroken, p4_moveGraphic, p4_moveTxt, p4_jumpGraphic, p4_jumpTxt, p4_atkGraphic, p4_atkTxt,
            p4_castTxt, p4_castGraphic, p4_breakWSpellcode, p4_gamba, ref p4_gambaActive);
    }

    private void UpdatePlayerOnboardingOnline(
        int playerIndex,
        bool usesJoinPrompt,
        ref bool joined,
        ref bool moveComplete,
        ref bool jumpComplete,
        ref bool atkComplete,
        ref bool glassBroken,
        Image moveGraphic,
        TextMeshProUGUI moveTxt,
        Image jumpGraphic,
        TextMeshProUGUI jumpTxt,
        Image atkGraphic,
        TextMeshProUGUI atkTxt,
        TextMeshProUGUI castTxt,
        Image castGraphic,
        SpriteRenderer breakWSpellcode,
        GambaMachine gamba,
        ref bool gambaActive)
    {
        if (gM.players == null || playerIndex >= gM.players.Length || gM.players[playerIndex] == null)
        {
            return;
        }

        PlayerController player = gM.players[playerIndex];
        InputSnapshot input = inputSnapshots[playerIndex];

        if (usesJoinPrompt && !joined)
        {
            SetText(atkTxt, "Attack");
            SetEnabled(atkTxt, false);
            SetEnabled(atkGraphic, false);
            SetEnabled(moveGraphic, true);
            SetEnabled(moveTxt, true);
            SetEnabled(jumpGraphic, true);
            SetEnabled(jumpTxt, true);
            joined = true;
        }

        if (!moveComplete && (input.Direction == 4 || input.Direction == 6))
        {
            moveComplete = true;
            SetColor(moveTxt, GameManager.colors["green"]);
            Debug.Log("Move Onboard Complete");
        }

        if (!jumpComplete && input.ButtonStates[1] == ButtonState.Pressed)
        {
            jumpComplete = true;
            SetColor(jumpTxt, GameManager.colors["green"]);
            Debug.Log("Jump Onboard Complete");
        }

        if (moveComplete && jumpComplete && !atkComplete)
        {
            SetEnabled(moveGraphic, false);
            SetEnabled(moveTxt, false);
            SetEnabled(jumpGraphic, false);
            SetEnabled(jumpTxt, false);
            SetEnabled(atkGraphic, true);
            SetEnabled(atkTxt, true);
            TryActivateGamba(gamba, ref gambaActive);

            if (player.basicsFired > 0)
            {
                atkComplete = true;
                Debug.Log("Atk Onboard Complete");
            }
        }

        if (atkComplete && player.spellList.Count == 0)
        {
            SetEnabled(atkGraphic, false);
            SetEnabled(atkTxt, false);
            SetEnabled(moveGraphic, false);
            SetEnabled(moveTxt, false);
            SetEnabled(jumpGraphic, false);
            SetEnabled(jumpTxt, false);
        }

        if (player.spellList.Count > 0 && !glassBroken)
        {
            SetEnabled(castTxt, true);
            SetEnabled(castGraphic, true);
            SetEnabled(atkGraphic, false);
            SetEnabled(atkTxt, false);
            SetEnabled(breakWSpellcode, true);

            if (input.ButtonStates[0] == ButtonState.Held)
            {
                SetSprite(castGraphic, inputGraphic);
                SetText(castTxt, "Input");
            }
            else
            {
                SetText(castTxt, "Hold");
                SetSprite(castGraphic, this.atkGraphic);
            }

            if (IsGateOpen(playerIndex))
            {
                glassBroken = true;
            }
        }

        if (glassBroken)
        {
            SetColor(castTxt, GameManager.colors["green"]);
            SetEnabled(castTxt, false);
            SetEnabled(castGraphic, false);
            SetEnabled(breakWSpellcode, false);
        }
    }

    private bool IsGateOpen(int playerIndex)
    {
        return gM.gates != null
            && playerIndex >= 0
            && playerIndex < gM.gates.Length
            && gM.gates[playerIndex] != null
            && gM.gates[playerIndex].isOpen;
    }

    private static void TryActivateGamba(GambaMachine gamba, ref bool gambaActive)
    {
        if (gambaActive || gamba == null)
        {
            return;
        }

        gamba.isActive = true;
        gambaActive = true;
    }

    private static void SetEnabled(Behaviour component, bool value)
    {
        if (component != null)
        {
            component.enabled = value;
        }
    }

    private static void SetEnabled(Renderer renderer, bool value)
    {
        if (renderer != null)
        {
            renderer.enabled = value;
        }
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void SetColor(TextMeshProUGUI text, Color value)
    {
        if (text != null)
        {
            text.color = value;
        }
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image != null)
        {
            image.sprite = sprite;
        }
    }

    private bool HasRequiredUiReferences()
    {
        return p1_moveGraphic != null && p1_moveTxt != null
            && p1_jumpGraphic != null && p1_jumpTxt != null
            && p1_atkGraphic != null && p1_atkTxt != null
            && p1_castGraphic != null && p1_castTxt != null
            && p1_breakWSpellcode != null && p1_gamba != null
            && p2_moveGraphic != null && p2_moveTxt != null
            && p2_jumpGraphic != null && p2_jumpTxt != null
            && p2_atkGraphic != null && p2_atkTxt != null
            && p2_castGraphic != null && p2_castTxt != null
            && p2_breakWSpellcode != null && p2_gamba != null
            && p3_moveGraphic != null && p3_moveTxt != null
            && p3_jumpGraphic != null && p3_jumpTxt != null
            && p3_atkGraphic != null && p3_atkTxt != null
            && p3_castGraphic != null && p3_castTxt != null
            && p3_breakWSpellcode != null && p3_gamba != null
            && p4_moveGraphic != null && p4_moveTxt != null
            && p4_jumpGraphic != null && p4_jumpTxt != null
            && p4_atkGraphic != null && p4_atkTxt != null
            && p4_castGraphic != null && p4_castTxt != null
            && p4_breakWSpellcode != null && p4_gamba != null;
    }
}

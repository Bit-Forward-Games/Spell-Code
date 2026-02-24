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

    public Image p1_moveGraphic;
    public TextMeshProUGUI p1_moveTxt;
    public Image p1_jumpGraphic;
    public TextMeshProUGUI p1_jumpTxt;
    public Image p1_atkGraphic;
    public TextMeshProUGUI p1_atkTxt;
    public Image p1_spellSlctGraphic;
    public TextMeshProUGUI p1_castTxt;
    public Image p1_castGraphic;

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

    public GambaMachine p4_gamba;
    private bool p4_gambaActive;

    private void Awake()
    {
        // if an instance already exists and it's not this one, destroy this duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            // otherwise, set this as the instance
            Instance = this;
            // optional: prevent the gameobject from being destroyed when loading new scenes
            DontDestroyOnLoad(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gM = GameManager.Instance;

        //properly set starting states for UI components
        p1_atkGraphic.enabled = false;
        p1_atkTxt.enabled = false;
        p1_spellSlctGraphic.enabled = false;
        p1_castGraphic.enabled = false;
        p1_castTxt.enabled = false;
        p1_gambaActive = false;

        p2_atkTxt.text = "Join";
        p2_moveGraphic.enabled = false;
        p2_moveTxt.enabled = false;
        p2_jumpGraphic.enabled = false;
        p2_jumpTxt.enabled = false;
        p2_spellSlctGraphic.enabled = false;
        p2_castGraphic.enabled = false;
        p2_castTxt.enabled = false;
        p2_gambaActive = false;

        p3_atkTxt.text = "Join";
        p3_moveGraphic.enabled = false;
        p3_moveTxt.enabled = false;
        p3_jumpGraphic.enabled = false;
        p3_jumpTxt.enabled = false;
        p3_spellSlctGraphic.enabled = false;
        p3_castGraphic.enabled = false;
        p3_castTxt.enabled = false;
        p3_gambaActive = false;

        p4_atkTxt.text = "Join";
        p4_moveGraphic.enabled = false;
        p4_moveTxt.enabled = false;
        p4_jumpGraphic.enabled = false;
        p4_jumpTxt.enabled = false;
        p4_spellSlctGraphic.enabled = false;
        p4_castGraphic.enabled = false;
        p4_castTxt.enabled = false;
        p4_gambaActive = false;
    }

    public void OnboardUpdate(ulong[] playerInputs)
    {
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
                if (inputSnapshots[0].Direction == 4 || inputSnapshots[0].Direction == 6) { p1_moveComplete = true; p1_moveTxt.color = Color.green; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p1_jumpComplete)
            {
                if (inputSnapshots[0].ButtonStates[1] == ButtonState.Pressed) { p1_jumpComplete = true; p1_jumpTxt.color = Color.green; Debug.Log("Jump Onboard Complete"); }
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
                    p1_gamba.gambaAnimator.SetBool("isActive", true); 
                    p1_gambaActive = true; 
                }

                if (p1_gamba.gambaAnimator.GetBool("isActive") == false) 
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

            }
            //hold atk and input code to break free
            if (gM.players[0].spellList.Count>0 && !p1_glassBroken)
            {
                p1_castTxt.enabled = true;
                p1_castGraphic.enabled = true;

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

                if (gM.players[0].spellsFired > 0)
                {
                    p1_glassBroken = true;
                }
            }

            if (p1_glassBroken)
            {
                p1_castTxt.color = Color.green;
                p1_castTxt.enabled = false;
                p1_castGraphic.enabled = false;
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
                if (inputSnapshots[1].Direction == 4 || inputSnapshots[1].Direction == 6) { p2_moveComplete = true; p2_moveTxt.color = Color.green; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p2_jumpComplete)
            {
                if (inputSnapshots[1].ButtonStates[1] == ButtonState.Pressed) { p2_jumpComplete = true; p2_jumpTxt.color = Color.green; Debug.Log("Jump Onboard Complete"); }
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
                    if (p2_gamba != null)
                    {
                        p2_gamba.gambaAnimator.SetBool("isActive", true);
                        p2_gambaActive = true;
                    }
                }

                if (p2_gamba != null && p2_gamba.gambaAnimator.GetBool("isActive") == false)
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
            }

            //hold atk and input code to break free
            if (gM.players[1].spellList.Count > 0 && !p2_glassBroken)
            {
                p2_castTxt.enabled = true;
                p2_castGraphic.enabled = true;

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

                if (gM.players[1].spellsFired > 0)
                {
                    p2_glassBroken = true;
                    p2_castTxt.enabled = false;
                    p2_castGraphic.enabled = false;
                }
            }

            if (p2_glassBroken)
            {
                p2_castTxt.color = Color.green;
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
                if (inputSnapshots[2].Direction == 4 || inputSnapshots[2].Direction == 6) { p3_moveComplete = true; p3_moveTxt.color = Color.green; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p3_jumpComplete)
            {
                if (inputSnapshots[2].ButtonStates[1] == ButtonState.Pressed) { p3_jumpComplete = true; p3_jumpTxt.color = Color.green; Debug.Log("Jump Onboard Complete"); }
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
                    p3_gamba.gambaAnimator.SetBool("isActive", true);
                    p3_gambaActive = true;
                }

                if (p3_gamba.gambaAnimator.GetBool("isActive") == false)
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

            }

            //hold atk and input code to break free
            if (gM.players[2].spellList.Count > 0 && !p3_glassBroken)
            {
                p3_castTxt.enabled = true;
                p3_castGraphic.enabled = true;

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

                if (gM.players[2].spellsFired > 0)
                {
                    p3_glassBroken = true;
                }
            }

            if (p3_glassBroken)
            {
                p3_castTxt.color = Color.green;
                p3_castTxt.enabled = false;
                p3_castGraphic.enabled = false;
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
                if (inputSnapshots[3].Direction == 4 || inputSnapshots[3].Direction == 6) { p4_moveComplete = true; p4_moveTxt.color = Color.green; Debug.Log("Move Onboard Complete"); }
            }

            //if player jumps
            if (!p4_jumpComplete)
            {
                if (inputSnapshots[3].ButtonStates[1] == ButtonState.Pressed) { p4_jumpComplete = true; p4_jumpTxt.color = Color.green; Debug.Log("Jump Onboard Complete"); }
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
                    p4_gamba.gambaAnimator.SetBool("isActive", true);
                    p4_gambaActive = true;
                }

                if (p4_gamba.gambaAnimator.GetBool("isActive") == false)
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

            }

            //hold atk and input code to break free
            if (gM.players[3].spellList.Count > 0 && !p4_glassBroken)
            {
                p4_castTxt.enabled = true;
                p4_castGraphic.enabled = true;

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

                if (gM.players[3].spellsFired > 0)
                {
                    p4_glassBroken = true;
                }
            }

            if (p4_glassBroken)
            {
                p4_castTxt.color = Color.green;
                p4_castTxt.enabled = false;
                p4_castGraphic.enabled = false;
            }
        }
    }
}

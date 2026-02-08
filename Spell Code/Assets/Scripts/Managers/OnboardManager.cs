using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnboardManager : MonoBehaviour
{
    //variables
    public InputSnapshot[] inputSnapshots = new InputSnapshot[4];

    private GameManager gM;

    //p1
    public bool p1_moveComplete = false;
    public bool p1_jumpComplete = false;
    public bool p1_atkComplete = false;
    public bool p1_scAcquired = false;
    public bool p1_glassBroken = false;

    public Image p1_moveGraphic;
    public TextMeshProUGUI p1_moveTxt;
    public Image p1_jumpGraphic;
    public TextMeshProUGUI p1_jumpTxt;
    public Image p1_atkGraphic;
    public TextMeshProUGUI p1_atkTxt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gM = GameManager.Instance;

        //properly set starting states for UI components
        p1_atkGraphic.enabled = false;
        p1_atkTxt.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

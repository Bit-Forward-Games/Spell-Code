using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerRamVals;
    public Image[] playerHpBar;
    public Image[] followPlayerHpBar;
    public Image[] playerDamageBar;
    public Image[] followPlayerDamageBar;
    public Image[] playerGoldBar;
    public GameObject[] emptyQuadrants;
    public Sprite[] spellOnCooldownIcon;
    public Sprite[] spellReadyIcon;
    public Sprite[] roundWinIcon;
    public Image[] flowStateVals;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] stockStabilityIcons;
    public Image[] demonAuraVals;
    public TextMeshProUGUI[] repsVals;
    public float flashAlpha = .5f;

    private float[] previousHealthFill; // track last known health per player
    private bool[] damageBarRunning;
    //public Image[] momentumVals;
    //public Image[] slimedVals;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Image[] followPlayerHpBar = new Image[4];
        Image[] followPlayerDamageBar = new Image[4];
        previousHealthFill = new float[4];
        damageBarRunning = new bool[4];
        for (int i = 0; i < 4; i++)
        {
            previousHealthFill[i] = 1f;
            damageBarRunning[i] = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateHealthVals();
    }

    public void UpdateHealthVals()
    {
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            // Transform childTransform = GameManager.Instance.players[i].transform.Find("Health Bar");
            // followPlayerHpBar[i] = childTransform.gameObject.GetComponent<Image>();
            followPlayerHpBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Health Bar").GetComponent<Image>();
            // playerRamVals[i].text = $"P{i + 1}  Total RAM: {GameManager.Instance.players[i].totalRam}\nRound RAM: {GameManager.Instance.players[i].roundRam} \nWins: {GameManager.Instance.players[i].roundsWon}";
            playerRamVals[i].text = $"{GameManager.Instance.players[i].roundRam}";
            //if (GameManager.Instance.players[i].isHit) StartCoroutine(DamageBar(i));

            float fillAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth) : 0;
            float fillGoldAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].roundRam / GameManager.Instance.ramNeededToWinRound) : 0;

            bool isRollback = RollbackManager.Instance != null && RollbackManager.Instance.isRollbackFrame;
            if (!isRollback && fillAmountVal < previousHealthFill[i] && !damageBarRunning[i])
            {
                StartCoroutine(DamageBar(i, previousHealthFill[i], fillAmountVal));
            }

            playerHpBar[i].fillAmount = fillAmountVal;
            followPlayerHpBar[i].fillAmount = fillAmountVal;
            previousHealthFill[i] = fillAmountVal;
            playerGoldBar[i].fillAmount = fillGoldAmountVal;

            emptyQuadrants[i].SetActive(false);

            flowStateVals[i].enabled = false;
            stockStabilityVals[i].enabled = false;
            demonAuraVals[i].enabled = false;
            repsVals[i].enabled = false;
            //momentumVals[i].enabled = false;
            //slimedVals[i].enabled = false;

            foreach (SpellData spell in GameManager.Instance.players[i].spellList)
            {
                if (spell.brands.Contains(Brand.VWave))
                {
                    flowStateVals[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.BigStox))
                {
                    stockStabilityVals[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.DemonX))
                {
                    demonAuraVals[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.Killeez))
                {
                    repsVals[i].enabled = true;
                }
                //if (spell.brands.Contains(Brand.Halk))
                //{
                //    momentumVals[i].enabled = true;
                //}

            }

            flowStateVals[i].enabled = true;
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / PlayerController.maxFlowState;

            stockStabilityVals[i].enabled = true;
            stockStabilityVals[i].text = GameManager.Instance.players[i].stockStability.ToString();
            
            demonAuraVals[i].enabled = true;
            demonAuraVals[i].fillAmount = (float)GameManager.Instance.players[i].demonAura / PlayerController.maxDemonAura;
            
            repsVals[i].enabled = true;
            repsVals[i].text = GameManager.Instance.players[i].reps.ToString();

            //momentumVals[i].enabled = true;
            //momentumVals[i].fillAmount = (float)GameManager.Instance.players[i].momentum / 100;

            //if (GameManager.Instance.players[i].slimed)
            //    slimedVals[i].enabled = true;
            //else slimedVals[i].enabled = false;
        }
    }

    public IEnumerator DamageBar(int playerIndex, float fromFill, float toFill)
    {
        // Transform childTransform = GameManager.Instance.players[playerIndex].transform.Find("Damage Bar");
        damageBarRunning[playerIndex] = true;

        followPlayerDamageBar[playerIndex] = FindChildContainingName(
            GameManager.Instance.players[playerIndex].gameObject, "Damage Bar").GetComponent<Image>();

        playerDamageBar[playerIndex].fillAmount = fromFill;
        followPlayerDamageBar[playerIndex].fillAmount = fromFill;

        yield return new WaitForSeconds(1f);

        float elapsedTime = 0f;
        float animationDuration = 1f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            playerDamageBar[playerIndex].fillAmount = Mathf.Lerp(fromFill, toFill, t);
            followPlayerDamageBar[playerIndex].fillAmount = Mathf.Lerp(fromFill, toFill, t);
            yield return null;
        }

        playerDamageBar[playerIndex].fillAmount = toFill;
        followPlayerDamageBar[playerIndex].fillAmount = toFill;

        damageBarRunning[playerIndex] = false;
    }

    GameObject FindChildContainingName(GameObject parent, string namePart)
    {
        // Get all child transforms (including grandchildren, etc.)
        Transform[] children = parent.GetComponentsInChildren<Transform>(true); 

        // Iterate through the children to find one whose name contains the specified part
        foreach (Transform childTransform in children)
        {
            // Exclude the parent itself from the search
            if (childTransform.gameObject == parent)
            {
                continue;
            }

            if (childTransform.name.Contains(namePart))
            {
                return childTransform.gameObject;
            }
        }
        return null; // No child found
    }
}

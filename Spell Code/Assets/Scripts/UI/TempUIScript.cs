using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerRamVals;
    public Image[] playerHpBar;
    public Image[] playerStoreBar;
    public Image[] followPlayerHpBar;
    public Image[] playerDamageBar;
    public Image[] followPlayerDamageBar;
    public Image[] playerGoldBar;
    public GameObject[] emptyQuadrants;
    public Sprite[] spellOnCooldownIcon;
    public Sprite[] spellReadyIcon;
    public Sprite[] roundWinIcon;
    public Image[] flowStateVals;
    public Image[] flowStateDim;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] stockStabilityIcons;
    public Image[] stockStabilityDim;
    public Image[] demonAuraVals;
    public Image[] demonAuraDim;
    public TextMeshProUGUI[] repsVals;
    public Image[] repsIcons;
    public Image[] repsDim;
    public float flashAlpha = .5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        followPlayerHpBar = new Image[4];
        followPlayerDamageBar = new Image[4];
        playerStoreBar = new Image[4];
    }

    // Update is called once per frame
    void Update()
    {
        UpdateUIBarVals();
    }

    public void UpdateUIBarVals()
    {
        for (int i = 0; i < GameManager.Instance.playerCount; i++)
        {
            followPlayerHpBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Health Bar").GetComponent<Image>();
            playerStoreBar[i] = FindChildContainingName(GameManager.Instance.players[i].gameObject, "Store Bar").GetComponent<Image>();
            // playerRamVals[i].text = $"P{i + 1}  Total RAM: {GameManager.Instance.players[i].totalRam}\nRound RAM: {GameManager.Instance.players[i].roundRam} \nWins: {GameManager.Instance.players[i].roundsWon}";
            playerRamVals[i].text = $"{GameManager.Instance.players[i].roundRam}";
            if (GameManager.Instance.players[i].isHit) StartCoroutine(DamageBar(i));

            float fillAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth) : 0;
            float fillGoldAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].roundRam / GameManager.Instance.ramNeededToWinRound) : 0;
            playerHpBar[i].fillAmount = fillAmountVal;
            followPlayerHpBar[i].fillAmount = fillAmountVal;
            playerGoldBar[i].fillAmount = fillGoldAmountVal;

            emptyQuadrants[i].SetActive(false);

            flowStateVals[i].enabled = false;
            stockStabilityVals[i].enabled = false;
            stockStabilityIcons[i].enabled = false;
            demonAuraVals[i].enabled = false;
            repsVals[i].enabled = false;
            repsIcons[i].enabled = false;

            flowStateDim[i].enabled = false;
            stockStabilityDim[i].enabled = false;
            demonAuraDim[i].enabled = false;
            repsDim[i].enabled = false;

            foreach (SpellData spell in GameManager.Instance.players[i].spellList)
            {
                if (spell.brands.Contains(Brand.VWave))
                {
                    flowStateVals[i].enabled = true;
                    flowStateDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.BigStox))
                {
                    stockStabilityVals[i].enabled = true;
                    stockStabilityIcons[i].enabled = true;
                    stockStabilityDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.DemonX))
                {
                    demonAuraVals[i].enabled = true;
                    demonAuraDim[i].enabled = true;
                }
                if (spell.brands.Contains(Brand.Killeez))
                {
                    repsVals[i].enabled = true;
                    repsIcons[i].enabled = true;
                    repsDim[i].enabled = true;
                }

            }

            // flowStateVals[i].enabled = true;
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / PlayerController.maxFlowState;

            // stockStabilityVals[i].enabled = true;
            // stockStabilityIcons[i].enabled = true;
            stockStabilityVals[i].text = GameManager.Instance.players[i].stockStability.ToString();
            
            // demonAuraVals[i].enabled = true;
            demonAuraVals[i].fillAmount = (float)GameManager.Instance.players[i].demonAura / PlayerController.maxDemonAura;
            
            // repsVals[i].enabled = true;
            // repsIcons[i].enabled = true;
            repsVals[i].text = GameManager.Instance.players[i].reps.ToString();

            if (repsVals[i].text == "0")
            {
                repsVals[i].enabled = false;
                repsIcons[i].enabled = false;
            }
            else if (repsVals[i].text != "0")
            {
                repsVals[i].enabled = true;
                repsIcons[i].enabled = true;
            }

            //Spell Store Bar
            float storeFillAmount = (float)GameManager.Instance.players[i].storedCodeDuration / 240;//TODO: change 240 to use the scale the bar length based on spell length
            playerStoreBar[i].fillAmount = storeFillAmount;
        }
    }

    public IEnumerator DamageBar(int playerIndex)
    {
        // Transform childTransform = GameManager.Instance.players[playerIndex].transform.Find("Damage Bar");
        followPlayerDamageBar[playerIndex] = FindChildContainingName(GameManager.Instance.players[playerIndex].gameObject, "Damage Bar").GetComponent<Image>();
        PlayerController player = GameManager.Instance.players[playerIndex];
        
        player.isHit = false;
        
        float previousHealthAmount = playerHpBar[playerIndex].fillAmount;
        
        float newHealthAmount = (float)player.currentPlayerHealth / player.charData.playerHealth;
        playerHpBar[playerIndex].fillAmount = newHealthAmount;
        
        playerDamageBar[playerIndex].fillAmount = previousHealthAmount;
        followPlayerDamageBar[playerIndex].fillAmount = previousHealthAmount;
        
        yield return new WaitForSeconds(1f);
        
        float elapsedTime = 0f;
        float animationDuration = 1f; 
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            playerDamageBar[playerIndex].fillAmount = Mathf.Lerp(previousHealthAmount, newHealthAmount, t);
            followPlayerDamageBar[playerIndex].fillAmount = Mathf.Lerp(previousHealthAmount, newHealthAmount, t);
            yield return null;
        }

        playerDamageBar[playerIndex].fillAmount = newHealthAmount;
        followPlayerDamageBar[playerIndex].fillAmount = newHealthAmount;
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

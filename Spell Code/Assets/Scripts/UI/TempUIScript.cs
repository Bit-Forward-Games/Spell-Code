using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerHpVals;
    public Image[] playerHpBar;
    public Image[] followPlayerHpBar;
    public Image[] playerDamageBar;
    public Image[] followPlayerDamageBar;
    public Image[] flowStateVals;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] demonAuraVals;
    public TextMeshProUGUI[] repsVals;
    public Image[] momentumVals;
    public Image[] slimedVals;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Image[] followPlayerHpBar = new Image[4];
        Image[] followPlayerDamageBar = new Image[4];
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
            playerHpVals[i].text = "P" + (i + 1);
            if (GameManager.Instance.players[i].isHit) StartCoroutine(DamageBar(i));

            float fillAmountVal = GameManager.Instance.players[i].charData != null? ((float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth) : 0;
            playerHpBar[i].fillAmount = fillAmountVal;
            followPlayerHpBar[i].fillAmount = fillAmountVal;

            flowStateVals[i].enabled = false;
            stockStabilityVals[i].enabled = false;
            demonAuraVals[i].enabled = false;
            repsVals[i].enabled = false;
            momentumVals[i].enabled = false;
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
                if (spell.brands.Contains(Brand.Halk))
                {
                    momentumVals[i].enabled = true;
                }

            }

            flowStateVals[i].enabled = true;
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / 300;

            stockStabilityVals[i].enabled = true;
            stockStabilityVals[i].text = "Crit: " + GameManager.Instance.players[i].stockStability;
            
            demonAuraVals[i].enabled = true;
            demonAuraVals[i].fillAmount = (float)GameManager.Instance.players[i].demonAura / 100;
            
            repsVals[i].enabled = true;
            repsVals[i].text = "REPS: " + GameManager.Instance.players[i].reps;

            momentumVals[i].enabled = true;
            momentumVals[i].fillAmount = (float)GameManager.Instance.players[i].momentum / 100;

            if (GameManager.Instance.players[i].slimed)
                slimedVals[i].enabled = true;
            else slimedVals[i].enabled = false;
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

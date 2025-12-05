using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerHpVals;
    public Image[] playerHpBar;
    public Image[] playerDamageBar;
    public Image[] flowStateVals;
    public TextMeshProUGUI[] stockStabilityVals;
    public Image[] demonAuraVals;
    public TextMeshProUGUI[] repsVals;
    public Image[] momentumVals;
    public Image[] slimedVals;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
            playerHpVals[i].text = "P" + (i + 1);
            if (GameManager.Instance.players[i].isHit) StartCoroutine(DamageBar(i));
            playerHpBar[i].fillAmount = (float)GameManager.Instance.players[i].currentPlayerHealth / GameManager.Instance.players[i].charData.playerHealth;


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
                if (spell.brands.Contains(Brand.RawrDX))
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
        PlayerController player = GameManager.Instance.players[playerIndex];
        
        player.isHit = false;
        
        float previousHealthAmount = playerHpBar[playerIndex].fillAmount;
        
        float newHealthAmount = (float)player.currentPlayerHealth / player.charData.playerHealth;
        playerHpBar[playerIndex].fillAmount = newHealthAmount;
        
        playerDamageBar[playerIndex].fillAmount = previousHealthAmount;
        
        yield return new WaitForSeconds(1f);
        
        float elapsedTime = 0f;
        float animationDuration = 1f; 
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            playerDamageBar[playerIndex].fillAmount = Mathf.Lerp(previousHealthAmount, newHealthAmount, t);
            yield return null;
        }

        playerDamageBar[playerIndex].fillAmount = newHealthAmount;
    }
}

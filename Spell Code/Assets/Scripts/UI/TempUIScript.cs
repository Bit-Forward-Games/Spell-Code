using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerHpVals;
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
            playerHpVals[i].text = "P" + (i + 1) + " HP: " + GameManager.Instance.players[i].currentPlayerHealth;

            flowStateVals[i].enabled = true;
            flowStateVals[i].fillAmount = (float)GameManager.Instance.players[i].flowState / 300;

            stockStabilityVals[i].enabled = true;
            stockStabilityVals[i].text = "CC: " + GameManager.Instance.players[i].stockStability;
            
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
}

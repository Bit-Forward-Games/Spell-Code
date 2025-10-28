using TMPro;
using UnityEngine;

public class TempUIScript : MonoBehaviour
{
    public TextMeshProUGUI[] playerHpVals;
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
            playerHpVals[i].text = "P" + (i + 1) + " HP: " + GameManager.Instance.players[i].currrentPlayerHealth;
        }
    }
}

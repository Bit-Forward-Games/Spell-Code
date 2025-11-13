using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    public PlayerController playerHealth;
    public Image healthBar;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
    //    playerHealth = GetComponent<PlayerController>();

    //    if (healthBar == null)
    //    {
    //        healthBar = GameObject.Find("Health Bar Fill").GetComponent<Image>();
    //    }
    //}

    // Update is called once per frame
    //void Update()
    //{
    //    if (playerHealth.isHit)
    //    {
    //        healthBar.fillAmount = playerHealth.currentPlayerHealth / 100f;
    //    }
    //}
}

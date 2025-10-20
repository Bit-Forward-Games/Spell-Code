using UnityEngine;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ShopManager : MonoBehaviour
{
    public Canvas shop;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //StartCoroutine(Shop());
        GameManager.Instance.isRunning = true;
        GameManager.Instance.ResetPlayers();
        SceneManager.LoadScene("Gameplay");
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public IEnumerator Shop()
    {
        yield return new WaitForSeconds(4);
        GameManager.Instance.isRunning = true;
        GameManager.Instance.ResetPlayers();
        SceneManager.LoadScene("Gameplay");
    }
}

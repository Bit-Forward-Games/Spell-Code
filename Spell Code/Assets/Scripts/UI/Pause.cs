using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public bool paused;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if (paused)
            {
                Resume();
            }
            else
            {
                Pausing();
            }
        }
    }

    public void Resume()
    {
        paused = false;
        pausemenu.SetActive(false);
        Time.timeScale = 1f;    
    }

    public void Pausing()
    {
        paused = true;
        pausemenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ReturnToLobby()
    {
        //paused = false;
        //pausemenu.SetActive(false);
        //Time.timeScale = 1f;
        //SceneManager.LoadScene("Lobby_Arena");

        //Resume game
        Resume();

        //Restart the game back at the lobby
        GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>().Restart();
        //GameManager.Instance.RestartGame();
    }

    public void QuitGame()
    {
        //save data
        DataManager.Instance.SaveToFile();

        //quit the game
        Debug.Log("Quitting Spell Code SlingerZ");
        Application.Quit();
    }
}

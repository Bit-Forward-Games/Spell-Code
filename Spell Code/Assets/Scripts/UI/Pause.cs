using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    public GameObject pausemenu;
    public bool paused;
    private SceneUiManager sceneUiManager;

    private void Start()
    {
        //find sceneUiManager
        sceneUiManager = GameObject.Find("pfb_GameManager").gameObject.GetComponent<SceneUiManager>();
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
        //Log a message
        Debug.Log("Game Resumed");

        paused = false;
        pausemenu.SetActive(false);
        Time.timeScale = 1f;    
    }

    public void Pausing()
    {
        //Log a message
        Debug.Log("Game Paused");

        paused = true;
        pausemenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ReturnToLobby()
    {
        //Log a message
        Debug.Log("Returning to Lobby");

        //Resume game
        Resume();

        //Restart the game back at the lobby
        sceneUiManager.MainMenu();
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

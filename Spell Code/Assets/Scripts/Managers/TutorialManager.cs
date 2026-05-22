using UnityEngine;

public class Tutorial : MonoBehaviour
{
    public GO_Door door;
    public GambaMachine machine;
    private GameManager gM;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gM = GameManager.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        door.CheckOpenDoor();
        if (door.CheckAllPlayersReady()) { gM.sceneManager.LoadScene("MainMenu"); }
    }
}

using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

public class web_post : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(Upload());
    }

    IEnumerator Upload()
    {
        using (UnityWebRequest www = UnityWebRequest.Post("https://cloud.mongodb.com/v2/68b88f14edca237048a44cf7#/explorer/68c99871055c01615155c4a5/Playtests/Sessions/find", "{ \"field1\": 1, \"field2\": 2 }", "application/json"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("Form Upload Complete!");
            }
        }
    }
}

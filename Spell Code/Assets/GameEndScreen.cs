//using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class GameEndScreen : MonoBehaviour
{
    public SpriteRenderer winnerImage = new SpriteRenderer();
    public TextMeshProUGUI winnerText = new TextMeshProUGUI();
    public Vector3 startingLocation = new Vector3(-2000f,77f,0f);
    public Vector3 targetLocation = new Vector3(380f,77f,0f);
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //make the player
        if(GameManager.Instance.bigWinner != null)
        {
            if(winnerText != null)
            {
                winnerText.text = $"Player {GameManager.Instance.bigWinner.pID} WINS!";
            }
            if (winnerImage != null)
            {
                MaterialPropertyBlock propertyBlock = new();
                winnerImage.GetPropertyBlock(propertyBlock);
                // Check if the property "_PaletteTex" exists in the shader
                if (winnerImage.sharedMaterial != null && winnerImage.sharedMaterial.HasProperty("_PaletteTex"))
                {
                    // Assign the palette texture to the property block
                    propertyBlock.SetTexture("_PaletteTex", GameManager.Instance.bigWinner.matchPalette[0]);
                    // Apply the updated property block back to the SpriteRenderer
                    winnerImage.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    Debug.LogWarning("Material does not have a '_PaletteTex' property.");
                }
            }
            else
            {
                Debug.LogError("SpriteRenderer is not assigned.");
            }

            
        }
        // if (winnerImage != null)
        // {
        //     winnerImage.transform.position = startingLocation;
        //     winnerImage.transform.DOMoveX(targetLocation.x, 5);
        // }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

using UnityEngine;
using DG.Tweening;
using TMPro;

public class GameEndScreen : MonoBehaviour
{
    [SerializeField] private SpriteRenderer winnerImage;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private Vector3 startingLocation = new Vector3(-15f, -1f, 0f);
    [SerializeField] private Vector3 targetLocation = new Vector3(3f, -1f, 0f);

    private void Start()
    {
        DisableEndSceneUi();
        ApplyWinnerSnapshot();

        if (winnerImage == null)
        {
            return;
        }

        winnerImage.transform.position = startingLocation;
        winnerImage.transform
            .DOMoveX(targetLocation.x, 2f)
            .OnComplete(EnableRestartInput);
    }

    private void DisableEndSceneUi()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }
    }

    private void ApplyWinnerSnapshot()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        if (winnerText != null && GameManager.Instance.endWinnerPid > 0)
        {
            winnerText.text = $"Player {GameManager.Instance.endWinnerPid} WINS!";
        }

        if (winnerImage == null)
        {
            Debug.LogError("GameEndScreen is missing winnerImage.");
            return;
        }

        Texture2D paletteTexture = GameManager.Instance.endWinnerPalette;
        if (paletteTexture == null)
        {
            return;
        }

        MaterialPropertyBlock propertyBlock = new();
        winnerImage.GetPropertyBlock(propertyBlock);

        if (winnerImage.sharedMaterial != null && winnerImage.sharedMaterial.HasProperty("_PaletteTex"))
        {
            propertyBlock.SetTexture("_PaletteTex", paletteTexture);
            winnerImage.SetPropertyBlock(propertyBlock);
        }
        else
        {
            Debug.LogWarning("Material does not have a '_PaletteTex' property.");
        }
    }

    private void EnableRestartInput()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.endInputEnabled = true;
        }
    }
}
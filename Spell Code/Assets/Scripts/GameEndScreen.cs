using UnityEngine;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameEndScreen : MonoBehaviour
{
    [SerializeField] private SpriteRenderer winnerImage;
    [SerializeField] private TextMeshProUGUI winnerText;
    public Vector3 startingLocation = new Vector3(-15f, -1f, 0f);
    public Vector3 targetLocation = new Vector3(3f, -1f, 0f);
    private bool useOnlineEndFlow;
    private bool restartTriggered;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        useOnlineEndFlow = GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive;
        restartTriggered = false;
        if (useOnlineEndFlow)
        {
            Time.timeScale = 1f;
            DisableOnlineEndUi();
        }

        ApplyWinnerPresentation(useOnlineEndFlow);

        if (winnerImage != null)
        {
            winnerImage.transform.position = startingLocation;
            Tween tween = winnerImage.transform.DOMoveX(targetLocation.x, 2f);
            if (useOnlineEndFlow)
            {
                tween.SetUpdate(true);
                tween.OnComplete(EnableRestartInput);
                StartCoroutine(EnableRestartInputFallback());
            }
            else
            {
                tween.OnComplete(EnableRestartInput);
            }
        }

    }

    private void Update()
    {
        if (!useOnlineEndFlow || restartTriggered || GameManager.Instance == null || !GameManager.Instance.endInputEnabled)
        {
            return;
        }

        if (!WasJumpPressedThisFrame())
        {
            return;
        }

        restartTriggered = true;
        GameManager.Instance.endInputEnabled = false;

        if (GameManager.Instance.sceneManager != null)
        {
            GameManager.Instance.sceneManager.MainMenu();
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }

    private void ApplyWinnerPresentation(bool useOnlineEndFlow)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int winnerPid = useOnlineEndFlow ? GameManager.Instance.endWinnerPid : (GameManager.Instance.bigWinner != null ? GameManager.Instance.bigWinner.pID : -1);
        Texture2D paletteTexture = useOnlineEndFlow
            ? GameManager.Instance.endWinnerPalette
            : (GameManager.Instance.bigWinner != null
                && GameManager.Instance.bigWinner.matchPalette != null
                && GameManager.Instance.bigWinner.pID - 1 >= 0
                && GameManager.Instance.bigWinner.pID - 1 < GameManager.Instance.bigWinner.matchPalette.Length
                    ? GameManager.Instance.bigWinner.matchPalette[GameManager.Instance.bigWinner.pID - 1]
                    : null);

        if (winnerText != null && winnerPid > 0)
        {
            winnerText.text = $"Player {winnerPid} WINS!";
            winnerText.gameObject.SetActive(true);
        }

        if (winnerImage == null)
        {
            Debug.LogError("SpriteRenderer is not assigned.");
            return;
        }

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

    private void DisableOnlineEndUi()
    {
        Selectable[] selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < selectables.Length; i++)
        {
            if (selectables[i] != null)
            {
                selectables[i].gameObject.SetActive(false);
            }
        }
    }

    public void EnableRestartInput()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.endInputEnabled = true;
        }
    }

    private IEnumerator EnableRestartInputFallback()
    {
        yield return new WaitForSecondsRealtime(2.1f);
        EnableRestartInput();
    }

    private bool WasJumpPressedThisFrame()
    {
        if (GameManager.Instance != null
            && GameManager.Instance.localPlayerIndex >= 0
            && GameManager.Instance.localPlayerIndex < GameManager.Instance.players.Length)
        {
            PlayerController localPlayer = GameManager.Instance.players[GameManager.Instance.localPlayerIndex];
            if (localPlayer != null
                && localPlayer.inputs != null
                && localPlayer.inputs.JumpAction != null
                && localPlayer.inputs.JumpAction.WasPressedThisFrame())
            {
                return true;
            }
        }

        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad gamepad = Gamepad.all[i];
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put one of these on each game mode button inside "Multiplayer Gamemodes Panel 2". It gives the
/// mode a wire-stable id and a label, and its <see cref="Select"/> method is what the button's
/// OnClick calls.
///
/// Wiring per mode button:
///   1. Add this component.
///   2. Set Mode Id to something short and permanent ("normal", "stock", "timed"). This string is
///      what travels between machines -- renaming it later breaks compatibility with older builds,
///      so treat it like a save key, not a label.
///   3. Set Display Name to what should appear in the Friends Lobby's "Selected GameMode" field.
///      Leave it empty to reuse the button's own text.
///   4. Point the button's OnClick at Select().
///
/// The host's pick is published to Steam lobby data, so every player in the lobby sees the same
/// mode name and starts the match on the same rules.
/// </summary>
[DisallowMultipleComponent]
public class OnlineGameModeOption : MonoBehaviour
{
    [Tooltip("Short, permanent identifier sent between machines. Not shown to players.")]
    [SerializeField] private string modeId = OnlineGameModeSelection.DefaultId;

    [Tooltip("Label shown in the Friends Lobby. Leave empty to use this button's own text.")]
    [SerializeField] private string displayName = "";

    [Tooltip("Optional. Shown while this mode is the host's current pick.")]
    [SerializeField] private GameObject selectedState;

    [Tooltip("Optional. Tinted between the two colours below as the selection changes.")]
    [SerializeField] private Graphic tintTarget;
    [SerializeField] private Color selectedTint = Color.white;
    [SerializeField] private Color unselectedTint = new Color(1f, 1f, 1f, 0.5f);

    public string ModeId => string.IsNullOrEmpty(modeId) ? OnlineGameModeSelection.DefaultId : modeId;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            // Fall back to whatever the button says, so a mode authored without a Display Name still
            // shows something meaningful instead of a raw id.
            TMPro.TextMeshProUGUI label = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                return label.text;
            }

            return ModeId;
        }
    }

    public OnlineGameModeSelection Selection => new OnlineGameModeSelection(ModeId, DisplayName);

    // The registry sweeps inactive objects (this panel starts disabled), so options do not register
    // themselves -- they just tell it to look again when the set of them could have changed.
    private void OnEnable()
    {
        OnlineGameModeRegistry.Invalidate();
    }

    private void OnDestroy()
    {
        OnlineGameModeRegistry.Invalidate();
    }

    /// <summary>Button OnClick. Makes this the host's chosen mode for the party lobby.</summary>
    public void Select()
    {
        SteamLobbyManager lobby = SteamLobbyManager.Instance;
        if (lobby == null)
        {
            Debug.LogWarning("[OnlineGameModeOption] Mode selected, but SteamLobbyManager was not found.", this);
            return;
        }

        if (!lobby.SetPartyGameMode(ModeId, DisplayName))
        {
            // Guests can look but not touch: only the lobby owner's pick is authoritative.
            return;
        }

        // Closing the panel and refreshing the label is the lobby panel's job; it polls the
        // selection every frame, so nothing else has to be pushed from here.
    }

    /// <summary>Called by PartyLobbyPanel each frame so the option can show whether it is the pick.</summary>
    public void SetSelectedVisual(bool isSelected)
    {
        if (selectedState != null && selectedState.activeSelf != isSelected)
        {
            selectedState.SetActive(isSelected);
        }

        if (tintTarget != null)
        {
            tintTarget.color = isSelected ? selectedTint : unselectedTint;
        }
    }
}

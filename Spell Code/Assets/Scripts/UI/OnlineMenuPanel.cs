using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Shared plumbing for the online menus that hang off the solo lobby's multiplayer door
/// (Online Play -> VS Friends / VS the World).
///
/// Two rules are baked in here because both have already cost this project a bug:
///  * The component must NOT live on the object it shows and hides. Deactivating your own
///    GameObject kills Update(), so the panel can never reopen itself and any menu it left open is
///    stranded (this is why Pause's panels sit outside TempUI). Awake refuses that wiring loudly.
///  * A UI submit read from Update is a render-vs-fixed race. Navigation works but the confirm
///    press is dropped on a vsynced build even though it lands in the editor, so confirms go
///    through Pause.TriggerSelectedButton() exactly like TempUIScript's gamemode menus do.
/// </summary>
public abstract class OnlineMenuPanel : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The GameObject to show/hide. Must be a DIFFERENT object than the one this component is on.")]
    [SerializeField] protected GameObject panelRoot;

    [Tooltip("Selectable that receives focus when the panel opens (gamepad/keyboard navigation).")]
    [SerializeField] protected GameObject firstSelected;

    [Tooltip("Freeze the game (timeScale 0) while this panel is up, like the other lobby menus.")]
    [SerializeField] protected bool freezeGameWhileOpen = true;

    /// <summary>
    /// How many online menu panels are currently up. GameManager consults this so a player cannot
    /// press start and join the local roster while someone is arranging an online match.
    /// </summary>
    public static int OpenPanelCount { get; private set; }

    public bool IsOpen { get; private set; }

    // Ancestors this panel switched on to make itself visible, so Close can put them back exactly
    // as it found them. See EnsureVisibleInHierarchy.
    private readonly System.Collections.Generic.List<GameObject> activatedAncestors =
        new System.Collections.Generic.List<GameObject>();

    protected TempUIScript TempUI => GameManager.Instance != null ? GameManager.Instance.tempUI : null;
    protected Pause PauseMenu => TempUI != null ? TempUI.pause : null;
    protected SteamLobbyManager Lobby => SteamLobbyManager.Instance;

    protected static bool IsOnlineMatchLive =>
        GameManager.Instance != null && GameManager.Instance.isOnlineMatchActive;

    // Statics survive "Enter Play Mode without domain reload", so a panel left open when play mode
    // stopped would leak into the next session's count and block player joining forever.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OpenPanelCount = 0;
    }

    protected virtual void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"[{GetType().Name}] panelRoot is not assigned; the panel cannot be shown or hidden.", this);
            return;
        }

        if (panelRoot == gameObject)
        {
            Debug.LogError($"[{GetType().Name}] panelRoot points at this component's own GameObject. Hiding it would stop Update() and the panel could never reopen. Put the component on a persistent parent and point panelRoot at the child panel.", this);
            panelRoot = null;
            return;
        }

        panelRoot.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        // Never leave the game frozen or the UI input devices scoped because this object went away
        // mid-transition (ExecuteOrder66 tears the lobby UI down while a panel can still be open).
        if (IsOpen)
        {
            Close();
        }
    }

    public void Open()
    {
        if (IsOpen || panelRoot == null)
        {
            return;
        }

        IsOpen = true;
        OpenPanelCount++;
        panelRoot.SetActive(true);
        EnsureVisibleInHierarchy();

        Pause pause = PauseMenu;
        TempUIScript tempUI = TempUI;
        if (pause != null && tempUI != null)
        {
            pause.ScopeUiInputToPlayerDevices(tempUI.ResolveGamemodesMenuPlayerIndex());
        }

        if (freezeGameWhileOpen)
        {
            Time.timeScale = 0f;
        }

        FocusFirstSelectable();
        OnOpened();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        OpenPanelCount = Mathf.Max(0, OpenPanelCount - 1);

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        OnClosed();

        // Only the last online panel to close hands the shared UI resources back; a panel that
        // closed to reveal another one underneath must not steal focus scoping from it.
        if (OpenPanelCount == 0)
        {
            RestoreAncestorsHiddenByOpen();
            PauseMenu?.RestoreScopedUiInputDevices();

            if (freezeGameWhileOpen)
            {
                Time.timeScale = 1f;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    protected virtual void OnOpened() { }

    protected virtual void OnClosed() { }

    // These panels live under TempUI's GameModesPanel, alongside the Solo/Multiplayer gamemode
    // panels -- and CloseGamemodeMenus() switches that container OFF. Every online entry runs it
    // (via CloseGamemodesMenuForOnlineEntry) immediately before leaving for MainMenu, which is
    // exactly where the Friends Lobby then has to appear: SetActive(true) on the panel alone would
    // leave it parented under a disabled container and it would never render.
    //
    // So switch on whatever ancestors are off, remember only the ones we touched, and put them back
    // when the last online panel closes. Walking up stops at the first already-active ancestor, so
    // nothing unrelated gets enabled.
    private void EnsureVisibleInHierarchy()
    {
        activatedAncestors.Clear();

        if (panelRoot == null || panelRoot.activeInHierarchy)
        {
            return;
        }

        Transform ancestor = panelRoot.transform.parent;
        while (ancestor != null && !panelRoot.activeInHierarchy)
        {
            if (!ancestor.gameObject.activeSelf)
            {
                ancestor.gameObject.SetActive(true);
                activatedAncestors.Add(ancestor.gameObject);
            }

            ancestor = ancestor.parent;
        }
    }

    private void RestoreAncestorsHiddenByOpen()
    {
        for (int i = activatedAncestors.Count - 1; i >= 0; i--)
        {
            if (activatedAncestors[i] != null)
            {
                activatedAncestors[i].SetActive(false);
            }
        }

        activatedAncestors.Clear();
    }

    protected void FocusFirstSelectable()
    {
        FocusSelectable(firstSelected);
    }

    protected void FocusSelectable(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Pause pause = PauseMenu;
        if (pause != null && pause.isActiveAndEnabled)
        {
            // One frame of real time before claiming focus, so a Selectable that was just enabled has
            // actually been registered with the EventSystem.
            pause.StartCoroutine(pause.SelectFirst(target));
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    /// <summary>
    /// Drives confirm/back for the panel. Call from Update while open. Confirms are routed through
    /// Pause so they use the same fixed-step-safe path as the rest of the lobby menus.
    /// </summary>
    protected void PollMenuInput(System.Action onCancel)
    {
        Pause pause = PauseMenu;
        if (pause == null || pause.paused)
        {
            return;
        }

        if (pause.WasPausePlayerSubmitPressedThisFrame())
        {
            pause.TriggerSelectedButton();
        }

        if (onCancel != null
            && (pause.WasPausePlayerCancelPressedThisFrame() || pause.WasPausePlayerBackPressedThisFrame()))
        {
            onCancel();
        }
    }

    protected static bool IsSteamReady()
    {
        return SteamLobbyManager.Instance != null && Steamworks.SteamClient.IsValid;
    }

    /// <summary>
    /// Finds a sibling menu controller without needing it dragged into the Inspector. These three
    /// components are all meant to sit on the same persistent object, so an unassigned reference is
    /// almost always "same GameObject" -- and a field typed as a component cannot accept the panel
    /// GameObject anyway, which makes the drag easy to get wrong. Checked in order: the assigned
    /// field, this GameObject, then anywhere in the scene (inactive included).
    ///
    /// Only called from button handlers, never per frame, so the scene search costs nothing in
    /// practice; the result is cached back into the field.
    /// </summary>
    protected T ResolveController<T>(ref T field) where T : Component
    {
        if (field != null)
        {
            return field;
        }

        field = GetComponent<T>();
        if (field == null)
        {
            field = FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        return field;
    }
}
